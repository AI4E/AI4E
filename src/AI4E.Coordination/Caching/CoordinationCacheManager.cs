using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Locking;
using AI4E.Coordination.Session;
using AI4E.Coordination.Storage;
using AI4E.Utils;
using AI4E.Utils.Async;
using static System.Diagnostics.Debug;
using static AI4E.Utils.DebugEx;

namespace AI4E.Coordination.Caching
{
    public sealed class CoordinationCacheManager : ICoordinationCacheManager
    {
        private readonly ICoordinationLockManager _lockManager;
        private readonly ICoordinationSessionOwner _sessionOwner;
        private readonly ISessionManager _sessionManager;
        private readonly ICoordinationStorage _storage;
        private readonly CoordinationEntryCache _cache = new CoordinationEntryCache();

        private readonly DisposableAsyncLazy<TaskCancellationTokenSource> _sessionTerminationSource;

        public CoordinationCacheManager(
            ICoordinationSessionOwner sessionOwner,
            ISessionManager sessionManager,
            ICoordinationStorage storage,
            ICoordinationLockManager lockManager)
        {
            if (sessionOwner == null)
                throw new ArgumentNullException(nameof(sessionOwner));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (lockManager == null)
                throw new ArgumentNullException(nameof(lockManager));

            _lockManager = lockManager;
            _sessionOwner = sessionOwner;
            _sessionManager = sessionManager;
            _storage = storage;

            _sessionTerminationSource = new DisposableAsyncLazy<TaskCancellationTokenSource>(
                factory: BuildSessionTerminationSourceAsync,
                disposal: DestroySessionTerminationSourceAsync,
                DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        private async Task<TaskCancellationTokenSource> BuildSessionTerminationSourceAsync(CancellationToken cancellation)
        {
            var session = await _sessionOwner.GetSessionAsync(cancellation);
            var sessionTermination = _sessionManager.WaitForTerminationAsync(session, cancellation);
            return new TaskCancellationTokenSource(sessionTermination);
        }

        private Task DestroySessionTerminationSourceAsync(TaskCancellationTokenSource sessionTerminationSource)
        {
            sessionTerminationSource.Dispose();
            return Task.CompletedTask;
        }

        #region Local Locks

        private ValueTask AcquireLocalReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);
            return cacheEntry.LocalReadLock.WaitAsync(cancellation).AsValueTask();
        }

        private ValueTask ReleaseLocalReadLockAsync(CoordinationEntryPath path)
        {
            var cacheEntry = _cache.GetEntry(path);
            Assert(cacheEntry != null);
            Assert(cacheEntry.LocalReadLock.CurrentCount == 0);

            if (cacheEntry.LocalWriteLock.Wait(0) && !cacheEntry.IsValid)
            {
                // We own both locks and the entry is invalid => We can remove it from the cache.
                _cache.RemoveEntry(path);
                cacheEntry.LocalWriteLock.Release();
            }

            cacheEntry.LocalReadLock.Release();
            return default;
        }

        // True if the lock could be taken immediately, false otherwise.
        private ValueTask<bool> AcquireLocalWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);
            var writeLock = cacheEntry.LocalWriteLock;
            return writeLock.LockOrWaitAsync(cancellation);
        }

        private async ValueTask ReleaseLocalWriteLockAsync(CoordinationEntryPath path)
        {
            var cacheEntry = _cache.GetEntry(path);
            var writeLock = cacheEntry.LocalWriteLock;
            Assert(writeLock.CurrentCount == 0);

#if DEBUG
            var entry = await _storage.GetEntryAsync(path, cancellation: default);
            var session = await _sessionOwner.GetSessionAsync(cancellation: default);

            // We must only release the local write-lock if we released the (global) write-lock first.
            Assert(entry == null || !await _sessionManager.IsAliveAsync(session, cancellation: default) || entry.WriteLock != session);
#endif

            if (cacheEntry.LocalReadLock.Wait(0) && !cacheEntry.IsValid)
            {
                // We own both locks and the entry is invalid => We can remove it from the cache.
                _cache.RemoveEntry(path);
                cacheEntry.LocalReadLock.Release();
            }

            writeLock.Release();
        }

        #endregion

        #region Locks

        public ValueTask<IStoredEntry> LockOrCreateEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            return AcquireWriteLockInternalAsync(path, unlockIfNonExistent: false, cancellation);
        }

        public ValueTask<IStoredEntry> LockEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            return AcquireWriteLockInternalAsync(path, unlockIfNonExistent: true, cancellation);
        }

        private async ValueTask<IStoredEntry> AcquireWriteLockInternalAsync(CoordinationEntryPath path, bool unlockIfNonExistent, CancellationToken cancellation)
        {
            var entry = await GetEntryAsync(path, cancellation);

            if (unlockIfNonExistent && entry == null)
                return null;

            // Enter local lock

            var atomicWait = await AcquireLocalWriteLockAsync(path, cancellation);

            try
            {
                // We had to wait for the local lock. Freshly load the entry from the database as the chances are great that our entry is outdated.
                if (!atomicWait)
                {
                    entry = await _storage.GetEntryAsync(path, cancellation);
                }

                // Enter global lock
                var result = await _lockManager.AcquireWriteLockAsync(entry, cancellation);

                // Release the local lock if the entry is null.
                if (unlockIfNonExistent && result == null)
                {
                    await ReleaseLocalWriteLockAsync(path);
                }

                Assert(result == null || result.WriteLock == await _sessionOwner.GetSessionAsync(cancellation));
                return result;
            }
            catch
            {
                // Release local lock on failure
                await ReleaseLocalWriteLockAsync(path);
                throw;
            }
        }

        public async ValueTask<IStoredEntry> UnlockEntryAsync(CoordinationEntryPath path)
        {
            var entry = await GetEntryAsync(path, cancellation: default);
            var cancellation = (await _sessionTerminationSource).Token;
            var session = await _sessionOwner.GetSessionAsync(cancellation);

            await AcquireLocalReadLockAsync(path, cancellation: default);

            try
            {
                try
                {
                    // TODO: Is it possible that entry is outdated and we own the write-lock? This may not happen, as we are releasing the local-lock afterwards.
                    if (entry != null && entry.WriteLock == session)
                    {
                        // Enter global read lock.
                        entry = await _lockManager.AcquireReadLockAsync(entry, cancellation);

                        // Release global write lock
                        entry = await _lockManager.ReleaseWriteLockAsync(entry);
                    }
                    else
                    {
                        entry = null;
                    }

                    Assert(entry == null || entry.WriteLock != await _sessionOwner.GetSessionAsync(cancellation));

                    await ReleaseLocalWriteLockAsync(path);

                    // As we did not invalidate the cache entry on write-lock acquirement, we have to update the cache.
                    // We must not update the cache, before we release the write lock, because it is not guaranteed, 
                    // that the write lock release is successful on the first attempt.
                    // We read the cache-entry before we release the write lock, to get the cache entry version and
                    // afterwards try to update the cache if no other session invalidated our cache entry in the meantime.
                    if (entry != null)
                    {
                        Assert(entry.ReadLocks.Contains(session));
                        _cache.UpdateEntry(entry);
                    }
                    else
                    {
                        // TODO: We could allocate a read-lock, if we do not have one, instead of invalidating the cache-entry.
                        // The cache entry is behind the global state and has to be invalidated, if not updated, to be consistent.
                        _cache.InvalidateEntry(path);
                    }

                    return entry;
                }
                catch (SessionTerminatedException) { throw; }
                catch
                {
                    _sessionOwner.Dispose();
                    throw;
                }
            }
            finally
            {
                await ReleaseLocalReadLockAsync(path);
            }
        }

        private async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            var path = entry.Path;

            // Enter local write lock, as we are modifying the stored entry with entering the read-lock
            var atomicWait = await AcquireLocalWriteLockAsync(path, cancellation);
            try
            {
                // We had to wait for the local lock. Freshly load the entry from the database as the chances are great that our entry is outdated.
                if (!atomicWait)
                {
                    entry = await _storage.GetEntryAsync(path, cancellation);
                }

                // Enter global read lock.
                return await _lockManager.AcquireReadLockAsync(entry, cancellation);
            }
            finally
            {
                await ReleaseLocalWriteLockAsync(path);
            }
        }

        private async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry)
        {
            Assert(entry != null);

            var path = entry.Path;
            var cancellation = (await _sessionTerminationSource).Token;

            try
            {
                // Enter local write lock, as we are modifying the stored entry with entering the read-lock
                var atomicWait = await AcquireLocalWriteLockAsync(path, cancellation);
                try
                {
                    // We had to wait for the local lock. Freshly load the entry from the database as the chances are great that our entry is outdated.
                    if (!atomicWait)
                    {
                        entry = await _storage.GetEntryAsync(path, cancellation);
                    }

                    // Release global read lock.
                    return await _lockManager.ReleaseReadLockAsync(entry);
                }
                finally
                {
                    await ReleaseLocalWriteLockAsync(path);
                }
            }
            catch
            {
                _sessionOwner.Dispose();
                throw;
            }
        }

        #endregion

        public async ValueTask<IStoredEntry> GetEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            // First try to load the entry from the cache.
            var cacheEntry = _cache.GetEntry(path);

            // We have to check whether the cache entry is (still) valid.
            if (cacheEntry.IsValid)
            {
                return cacheEntry.Entry;
            }

            await AcquireLocalReadLockAsync(path, cancellation);

            try
            {
                var entry = await _storage.GetEntryAsync(path, cancellation);

                if (entry == null)
                {
                    return null;
                }

                entry = await AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    return null;
                }

                try
                {
#if DEBUG
                    var session = await _sessionOwner.GetSessionAsync(cancellation);
                    Assert(entry.ReadLocks.Contains(session));
#endif
                    var updatedCacheEntry = _cache.UpdateEntry(entry);

                    // If we cannot update the cache, f.e. due to an old cache entry version, the cache is invalidated, and we do not need the read-lock. => Free it.
                    // This must be synchronized with any concurrent read operations that 
                    // (1) Register a read-lock (No-op, as we currently own a read-lock)
                    // (2) Updates the cache entry successfully.
                    // (3) We are releasing the read-lock here
                    // In order to not leaves the coordination service in an inconsistent state, we have acquire a separate local read-lock on cache update.
                    if (!updatedCacheEntry.IsValid)
                    {
                        Assert(entry != null);
                        await ReleaseReadLockAsync(entry);
                    }
                }
                catch
                {
                    Assert(entry != null);
                    await ReleaseReadLockAsync(entry);
                    throw;
                }

                return entry;
            }
            finally
            {
                await ReleaseLocalReadLockAsync(path);
            }
        }

        public async ValueTask InvalidateEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);

            if (!cacheEntry.TryGetEntry(out var entry))
            {
                // If the entry is not in the cache, we cannot asume that we do not own a read-lock for it.
                entry = await _storage.GetEntryAsync(path, cancellation);
            }

            try
            {
                await AcquireLocalReadLockAsync(path, cancellation);

                _cache.InvalidateEntry(path);
                await ReleaseReadLockAsync(entry);
            }
            finally
            {
                await ReleaseLocalReadLockAsync(path);
            }
        }

        /// <summary>
        /// The datastructure the coordination service uses to cache entries.
        /// </summary>
        /// <remarks>This type is thread-safe.</remarks>
        private sealed class CoordinationEntryCache
        {
            private readonly ConcurrentDictionary<CoordinationEntryPath, CacheEntry> _cache;

            /// <summary>
            /// Creates a new instance of the <see cref="CoordinationEntryCache"/> type.
            /// </summary>
            public CoordinationEntryCache()
            {
                _cache = new ConcurrentDictionary<CoordinationEntryPath, CacheEntry>();
            }

            /// <summary>
            /// Gets the cache entry with the specified path.
            /// </summary>
            /// <param name="path">The path of the cache entry.</param>
            /// <returns>The cache entry.</returns>
            /// <remarks>
            /// This operation does always return a cache entry and never returns null.
            /// If no cache entry with the specifies path can be found in the cache, 
            /// an invalidated entry is inserted.
            /// </remarks>
            public CacheEntry GetEntry(CoordinationEntryPath path)
            {
                return _cache.GetOrAdd(path, _ => new CacheEntry(path));
            }

            /// <summary>
            /// Invalidates the cache entry with the specified path.
            /// </summary>
            /// <param name="path">The path of the cache entry.</param>
            /// <returns>The invalidated cache entry.</returns>
            public CacheEntry InvalidateEntry(CoordinationEntryPath path)
            {
                return _cache.AddOrUpdate(path, _ => new CacheEntry(path), (_, e) => e.Invalidate());
            }

            /// <summary>
            /// Updates the cache entry with the specified stored entry if the version matches.
            /// </summary>
            /// <param name="entry">The stored entry.</param>
            /// <returns>The updated cache entry.</returns>
            /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is null.</exception>
            /// <remarks>
            /// It is NOT guaranteed that the returned cache entry is valid and the <see cref="CacheEntry.Entry"/> property accessable.
            /// </remarks>
            public CacheEntry UpdateEntry(IStoredEntry entry)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                var path = entry.Path;

                do
                {
                    if (!_cache.TryGetValue(path, out var current))
                    {
                        current = null;
                    }

                    var start = current;

                    if (start == null)
                    {
                        var desired = new CacheEntry(entry.Path, entry);

                        if (_cache.TryAdd(path, desired))
                        {
                            return desired;
                        }
                    }
                    else
                    {
                        var desired = start.Update(entry);

                        // Nothing to change in the cache. We are done.
                        if (start == desired)
                        {
                            return desired;
                        }

                        if (_cache.TryUpdate(path, desired, start))
                        {
                            return desired;
                        }
                    }
                }
                while (true);
            }

            /// <summary>
            /// Removes the cache entry with the specified path from the cache.
            /// </summary>
            /// <param name="path">The path of the cache entry.</param>
            /// <returns>The removed cache entry or null if the cache does not contain a cache entry with the specified path.</returns>
            public CacheEntry RemoveEntry(CoordinationEntryPath path)
            {
                if (!_cache.TryRemove(path, out var result))
                {
                    result = null;
                }

                return result;
            }
        }

        /// <summary> Represent a cache entry. </summary>
        /// <remarks> This type is thread-safe. </remarks>
        private sealed class CacheEntry
        {
            private readonly IStoredEntry _entry;

            internal CacheEntry(CoordinationEntryPath path)
            {
                Assert(path != null);

                Path = path;
                _entry = null;
                LocalReadLock = CreateLocalLock();
                LocalWriteLock = CreateLocalLock();
            }

            internal CacheEntry(CoordinationEntryPath path, IStoredEntry entry)
            {
                Assert(path != null);
                Assert(entry != null);

                Path = path;
                _entry = entry;
                LocalReadLock = CreateLocalLock();
                LocalWriteLock = CreateLocalLock();
            }

            private CacheEntry(CoordinationEntryPath path,
                               IStoredEntry entry,
                               SemaphoreSlim localReadLock,
                               SemaphoreSlim localWriteLock)
            {
                Assert(path != null);
                Assert(localReadLock != null);
                Assert(localWriteLock != null);

                Path = path;
                _entry = entry;
                LocalReadLock = localReadLock;
                LocalWriteLock = localWriteLock;
            }

            private static SemaphoreSlim CreateLocalLock()
            {
                return new SemaphoreSlim(1);
            }

            /// <summary>
            /// The path of the entry, the cache entry stores.
            /// </summary>
            public CoordinationEntryPath Path { get; }

            /// <summary>
            /// A boolean value indicating whether the cache entry is valid and <see cref="Entry"/> can be used safely.
            /// </summary>
            public bool IsValid => _entry != null;

            /// <summary>
            /// The stored entry, the cache entry stored.
            /// </summary>
            /// <exception cref="InvalidOperationException">Thrown if the cache entry is invalidated.</exception>
            public IStoredEntry Entry
            {
                get
                {
                    if (!IsValid)
                    {
                        throw new InvalidOperationException();
                    }

                    return _entry;
                }
            }

            // The local read-lock protects the cache-entry from beeing modified (invalidated/updated) concurrently.
            // It has to be ensured that we always own a (global) read-lock for an entry if it is present in the cache.
            // The following situation has to be prevented:
            // 1) a read operation tries to update the cache and
            // 2) another session concurrently writes to the entry, invalidating our cache entry
            // Without synchronization the cache update of operation (1) and the cache invalidation of operation (2)
            // may be performed out of order, leaving the cache with a non-invalidated entry but no read-lock aquired.
            // The update and invalidate operations have to be performed in this way:
            // Update:
            // (1) Lock local read-lock [Begin of critical section]
            // (2) Aquire (global) read-lock
            // (3) Update cache entry
            // (4) Unlock local read-lock [End of critical section]
            // Invalidate:
            // (1) Lock local read-lock [Begin of critical section]
            // (2) Invalidate cache entry
            // (3) Release (global) read-lock
            // (4) Unlock local read-lock [End of critical section]
            // Read-operation do not have to be protected, as there is no chance
            // that a cache entry can be read without owning the (global) read-lock for it.

            /// <summary>
            /// Gets the local read lock of the cache entry.
            /// </summary>
            public SemaphoreSlim LocalReadLock { get; }


            /// <summary>
            /// Gets the local write lock of the cache entry.
            /// </summary>
            public SemaphoreSlim LocalWriteLock { get; }

            public bool TryGetEntry(out IStoredEntry entry)
            {
                entry = _entry;
                return IsValid;
            }

            internal CacheEntry Invalidate()
            {
                return new CacheEntry(Path, entry: null, localReadLock: LocalReadLock, localWriteLock: LocalWriteLock);
            }

            internal CacheEntry Update(IStoredEntry entry)
            {
                if (
                    entry == null ||
                    IsValid && Entry != null && Entry.StorageVersion > entry.StorageVersion)
                {
                    return this;
                }

                return new CacheEntry(Path, entry, localReadLock: LocalReadLock, localWriteLock: LocalWriteLock);
            }
        }
    }

    public static class SemaphoreSlimExtension
    {
        // True if the lock could be taken immediately, false otherwise.
        public static ValueTask<bool> LockOrWaitAsync(this SemaphoreSlim semaphore, CancellationToken cancellation)
        {
            if (semaphore.Wait(0))
            {
                Assert(semaphore.CurrentCount == 0);
                return new ValueTask<bool>(true);
            }

            return WaitAsync(semaphore, cancellation).AsValueTask();
        }

        private static async Task<bool> WaitAsync(SemaphoreSlim semaphore, CancellationToken cancellation)
        {
            await semaphore.WaitAsync(cancellation);
            Assert(semaphore.CurrentCount == 0);

            return false;
        }
    }
}
