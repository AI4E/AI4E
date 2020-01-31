using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Coordination
{
    internal sealed class CoordinationLockManager : ICoordinationLockManager
    {
        #region Fields

        private readonly ICoordinationSessionOwner _sessionOwner;
        private readonly ISessionManager _sessionManager;
        private readonly CoordinationEntryCache _cache;
        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ICoordinationWaitManager _waitManager;
        private readonly ICoordinationExchangeManager _exchangeManager;
        private readonly ILogger<CoordinationLockManager> _logger;

        private readonly DisposableAsyncLazy<TaskCancellationTokenSource> _sessionTerminationSource;

        #endregion

        #region C'tor

        public CoordinationLockManager(ICoordinationSessionOwner sessionOwner,
                                       ISessionManager sessionManager,
                                       CoordinationEntryCache cache,
                                       ICoordinationStorage storage,
                                       IStoredEntryManager storedEntryManager,
                                       ICoordinationWaitManager waitManager,
                                       ICoordinationExchangeManager exchangeManager,
                                       ILogger<CoordinationLockManager> logger = null)
        {
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            if (waitManager == null)
                throw new ArgumentNullException(nameof(waitManager));

            if (exchangeManager == null)
                throw new ArgumentNullException(nameof(exchangeManager));

            if (sessionOwner == null)
                throw new ArgumentNullException(nameof(sessionOwner));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            _sessionOwner = sessionOwner;
            _sessionManager = sessionManager;
            _cache = cache;
            _storage = storage;
            _storedEntryManager = storedEntryManager;
            _waitManager = waitManager;
            _exchangeManager = exchangeManager;
            _logger = logger;

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

        #endregion

        #region ICoordinationLockManager

        public Task AcquireLocalWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            return AcquireLocalWriteLockInternalAsync(path, cancellation);
        }

        public Task ReleaseLocalWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            return ReleaseLocalWriteLockInternalAsync(path, cancellation);
        }

        public async Task AcquireLocalReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);
            await cacheEntry.LocalReadLock.WaitAsync(cancellation);
        }

        public Task ReleaseLocalReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);
            Debug.Assert(cacheEntry != null);
            Debug.Assert(cacheEntry.LocalReadLock.CurrentCount == 0);

            cacheEntry.LocalReadLock.Release();
            return Task.CompletedTask;
        }

        // Acquired a read lock for the entry with the specified path and returns the entry.
        // If the result is null, the entry does not exist and no lock is allocated.
        public async Task<IStoredEntry> AcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var path = entry.Path;


            // Enter local lock

            var atomicWait = await AcquireLocalWriteLockInternalAsync(path, cancellation);
            try
            {
                // We had to wait for the local lock. Freshly load the entry from the database as the chances are great that our entry is outdated.
                if (!atomicWait)
                {
                    entry = await _storage.GetEntryAsync(path, cancellation);
                }

                // Enter global lock
                var result = await InternalAcquireWriteLockAsync(entry, cancellation);

                // Release the local lock if the entry is null.
                if (result == null)
                {
                    await ReleaseLocalWriteLockInternalAsync(path, cancellation: default);
                }

                return result;
            }
            catch
            {
                // Release local lock on failure
                await ReleaseLocalWriteLockInternalAsync(path, cancellation: default);
                throw;
            }
        }

        // Releases the write lock for the specified entry and returns the updated entry.
        // If the current session does not own the write-lock for the entry (f.e. if it is deleted), 
        // this method only releases the local lock but is a no-op otherwise.
        public Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return ReleaseWriteLockAsync(entry).WithCancellation(cancellation);
        }

        public async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var path = entry.Path;

            // Enter local write lock, as we are modifying the stored entry with entering the read-lock
            var atomicWait = await AcquireLocalWriteLockInternalAsync(path, cancellation);
            try
            {
                // We had to wait for the local lock. Freshly load the entry from the database as the chances are great that our entry is outdated.
                if (!atomicWait)
                {
                    entry = await _storage.GetEntryAsync(path, cancellation);
                }

                // Enter global read lock.
                return await InternalAcquireReadLockAsync(entry, cancellation);
            }
            finally
            {
                await ReleaseLocalWriteLockInternalAsync(path, cancellation);
            }
        }

        public Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return ReleaseReadLockAsync(entry).WithCancellation(cancellation);
        }

        #endregion

        private SemaphoreSlim GetWriteLock(CoordinationEntryPath path)
        {
            var cacheEntry = _cache.GetEntry(path);
            return cacheEntry.LocalWriteLock;
        }

        // True if the lock could be taken immediately, false otherwise.
        private Task<bool> AcquireLocalWriteLockInternalAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var writeLock = GetWriteLock(path);

            if (writeLock.Wait(0))
            {
                Debug.Assert(writeLock.CurrentCount == 0);
                return Task.FromResult(true);
            }

            return AcquireLocalWriteLockCoreAsync(writeLock, cancellation);
        }

        private async Task<bool> AcquireLocalWriteLockCoreAsync(SemaphoreSlim writeLock, CancellationToken cancellation)
        {
            await writeLock.WaitAsync(cancellation);
            Debug.Assert(writeLock.CurrentCount == 0);

            return false;
        }

        private
#if DEBUG
            async
#endif
            Task ReleaseLocalWriteLockInternalAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var writeLock = GetWriteLock(path);
            Debug.Assert(writeLock.CurrentCount == 0);

#if DEBUG
            var entry = await _storage.GetEntryAsync(path, cancellation);
            var session = await _sessionOwner.GetSessionAsync(cancellation);

            // We must only release the local write-lock if we released the (global) write-lock first.
            Debug.Assert(entry == null || !await _sessionManager.IsAliveAsync(session, cancellation) || entry.WriteLock != session);
#endif

            writeLock.Release();
#if !DEBUG
            return Task.CompletedTask;
#endif
        }

        private async Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry)
        {
            Debug.Assert(entry != null);
            try
            {
                var result = await InternalReleaseWriteLockAsync(entry);

                Debug.Assert(result == null || result.WriteLock != await _sessionOwner.GetSessionAsync(cancellation: default));

                await ReleaseLocalWriteLockInternalAsync(entry.Path, cancellation: default);
                return result;
            }
            catch (SessionTerminatedException) { throw; }
            catch
            {
                _sessionOwner.Dispose();
                throw;
            }
        }

        private async Task<IStoredEntry> InternalAcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Stopwatch watch = null;
            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            var session = await _sessionOwner.GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
            {
                throw new SessionTerminatedException(session);
            }

            if (entry != null)
            {
                _logger?.LogTrace($"[{session}] Acquiring write-lock for entry '{entry.Path}'.");
            }

            // We have to perform the operation in a CAS-like loop because concurrency is controlled via a version number only.
            IStoredEntry start, desired;
            do
            {
                // We wait till we are able to lock. This means we need to wait till the write lock is free.
                start = await _waitManager.WaitForWriteLockReleaseAsync(entry, allowWriteLock: false, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Debug.Assert(start.WriteLock == null);

                // Actually try to lock the entry.
                // Do not use UpdateEntryAsync as this method assumes that we already own the write-lock.
                desired = _storedEntryManager.AcquireWriteLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (entry != start);

            // If we reached this point, we own the write lock.
            entry = desired;
            Debug.Assert(entry != null);

            try
            {
                _logger?.LogTrace($"[{session}] Pending write-lock for entry '{entry.Path}'. Waiting for read-locks to release.");

                // Wait till all read-locks are freed.
                entry = await _waitManager.WaitForReadLocksReleaseAsync(entry, cancellation);

                // We hold the write lock. No-one can alter the entry except our session is terminated. But this will cause WaitForReadLocksReleaseAsync to throw.
                Debug.Assert(entry != null);

                // We own the write-lock.
                // All read-locks must be free except for ourself.
                Debug.Assert(entry.WriteLock == session);
                Debug.Assert(entry.ReadLocks.IsEmpty || entry.ReadLocks.Length == 1 && entry.ReadLocks[0] == session);

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    Debug.Assert(watch != null);
                    watch.Stop();

                    _logger?.LogTrace($"[{session}] Acquired write-lock for entry '{entry.Path}' in {watch.Elapsed.TotalSeconds}sec.");
                }

                return entry;
            }
            catch
            {
                try
                {
                    await InternalReleaseWriteLockAsync(entry);
                }
                catch (SessionTerminatedException) { throw; }
                catch
                {
                    _sessionOwner.Dispose();
                    throw;
                }

                throw;
            }
        }

        private async Task<IStoredEntry> InternalReleaseWriteLockAsync(IStoredEntry entry)
        {
            Debug.Assert(entry != null);

            var path = entry.Path;
            var cancellation = (await _sessionTerminationSource).CancellationToken;
            IStoredEntry start, desired;

            var session = await _sessionOwner.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{session}] Releasing write-lock for entry '{path}'.");
            }

            CacheEntry cacheEntry;

            do
            {
                start = entry;

                // The entry was deleted.
                if (start == null)
                {
                    _exchangeManager.NotifyWriteLockReleasedAsync(path, cancellation).HandleExceptions(_logger);
                    _logger?.LogTrace($"[{session}] Released write-lock for entry '{path}'.");
                    _cache.InvalidateEntry(path);
                    return start;
                }

                // The session does not own the write lock.
                if (start.WriteLock != session)
                {
                    return start;
                }

                // As we did not invalidate the cache entry on write-lock acquirement, we have to update the cache.
                // We must not update the cache, before we released the write lock, because it is not guaranteed, 
                // that the write lock release is successful on the first attempt.
                // We read the cache-entry before we release the write lock, to get the cache entry version and
                // afterwards try to update the cache if no other session invalidated our cache entry in the meantime.
                cacheEntry = _cache.GetEntry(path);
                desired = _storedEntryManager.ReleaseWriteLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (entry != start);

            entry = desired;
            Debug.Assert(entry != null);
            Debug.Assert(entry.WriteLock == null);

            if (entry.ReadLocks.Contains(session))
            {
                Debug.Assert(entry.ReadLocks.Contains(session));
                _cache.UpdateEntry(cacheEntry, entry);
            }
            else
            {
                // TODO: We could allocate a read-lock, if we do not have one, instead of invalidating the cache-entry.
                // The cache entry is behind the global state and has to be invalidated, if not updated, to be consistent.
                _cache.InvalidateEntry(cacheEntry);
            }

            if (entry != null)
            {
                _exchangeManager.NotifyWriteLockReleasedAsync(path, cancellation).HandleExceptions(_logger);
                _logger?.LogTrace($"[{session}] Released write-lock for entry '{path}'.");
            }

            return entry;
        }

        private async Task<IStoredEntry> InternalAcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Stopwatch watch = null;
            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            IStoredEntry start, desired;

            var session = await _sessionOwner.GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
            {
                throw new SessionTerminatedException(session);
            }

            if (entry != null)
            {
                _logger?.LogTrace($"[{session}] Acquiring read-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = await _waitManager.WaitForWriteLockReleaseAsync(entry, allowWriteLock: true, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Debug.Assert(start.WriteLock == null || start.WriteLock == session);

                desired = _storedEntryManager.AcquireReadLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);

            entry = desired;
            Debug.Assert(entry != null);
            Debug.Assert(entry.ReadLocks.Contains(session));

            try
            {
                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    Debug.Assert(watch != null);
                    watch.Stop();

                    _logger?.LogTrace($"[{session}] Acquired read-lock for entry '{entry.Path.EscapedPath}' in {watch.ElapsedMilliseconds}ms.");
                }

                return entry;
            }
            catch
            {
                Debug.Assert(entry != null);

                // Release global read lock on failure.
                await ReleaseReadLockAsync(entry);
                throw;
            }
        }

        private async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry)
        {
            var path = entry.Path;

            try
            {
                // Enter local write lock, as we are modifying the stored entry with entering the read-lock
                var atomicWait = await AcquireLocalWriteLockInternalAsync(path, cancellation: default);
                try
                {
                    // We had to wait for the local lock. Freshly load the entry from the database as the chances are great that our entry is outdated.
                    if (!atomicWait)
                    {
                        entry = await _storage.GetEntryAsync(path, cancellation: default);
                    }

                    // Release global read lock.
                    return await InternalReleaseReadLockAsync(entry);
                }
                finally
                {
                    await ReleaseLocalWriteLockInternalAsync(path, cancellation: default);
                }
            }
            catch
            {
                _sessionOwner.Dispose();
                throw;
            }
        }

        private async Task<IStoredEntry> InternalReleaseReadLockAsync(IStoredEntry entry)
        {
            Debug.Assert(entry != null);

            var cancellation = (await _sessionTerminationSource).CancellationToken;
            IStoredEntry start, desired;

            var session = await _sessionOwner.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{session}] Releasing read-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = entry;

                // The entry was deleted (concurrently).
                if (start == null || !start.ReadLocks.Contains(session))
                {
                    return null;
                }

                desired = _storedEntryManager.ReleaseReadLock(start, session);

                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);

            Debug.Assert(entry != null);

            _exchangeManager.NotifyReadLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
            _logger?.LogTrace($"[{session}] Released read-lock for entry '{entry.Path}'.");

            return desired;
        }
    }
}
