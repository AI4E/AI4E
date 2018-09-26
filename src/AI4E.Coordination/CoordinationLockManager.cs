using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

// TODO: Release lock cancellation

namespace AI4E.Coordination
{
    internal sealed class CoordinationLockManager : ICoordinationLockManager
    {
        #region Fields

        private readonly ICoordinationManager _coordinationManager;
        private readonly CoordinationEntryCache _cache;
        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ICoordinationWaitManager _waitManager;
        private readonly ICoordinationExchangeManager _exchangeManager;
        private readonly ILogger _logger;

        #endregion

        #region C'tor

        public CoordinationLockManager(ICoordinationManager coordinationManager,
                           CoordinationEntryCache cache,
                           ICoordinationStorage storage,
                           IStoredEntryManager storedEntryManager,
                           ICoordinationWaitManager waitManager,
                           ICoordinationExchangeManager exchangeManager,
                           ILogger logger = null)
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

            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            _coordinationManager = coordinationManager;
            _cache = cache;
            _storage = storage;
            _storedEntryManager = storedEntryManager;
            _waitManager = waitManager;
            _exchangeManager = exchangeManager;
            _logger = logger;
        }

        #endregion

        #region ICoordinationLockManager

        public async Task AcquireLocalLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);
            await cacheEntry.LocalLock.WaitAsync(cancellation);
        }

        public Task ReleaseLocalLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);

            Assert(cacheEntry != null);
            Assert(cacheEntry.LocalLock.CurrentCount == 0);

            cacheEntry.LocalLock.Release();
            return Task.CompletedTask;
        }

        // Acquired a read lock for the entry with the specified path and returns the entry.
        // If the result is null, the entry does not exist and no lock is allocated.
        public async Task<IStoredEntry> AcquireWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var cacheEntry = _cache.GetEntry(path);

            // Enter local lock
            await cacheEntry.LocalLock.WaitAsync(cancellation);

            try
            {
                var entry = cacheEntry.IsValid ? cacheEntry.Entry : await _storage.GetEntryAsync(path, cancellation);

                // Enter global lock
                var result = await InternalAcquireWriteLockAsync(entry, cancellation);

                // Release the local lock if the entry is null.
                if (result == null)
                {
                    Assert(cacheEntry.LocalLock.CurrentCount == 0);
                    cacheEntry.LocalLock.Release();
                }

                return result;
            }
            catch
            {
                Assert(cacheEntry.LocalLock.CurrentCount == 0);
                // Release local lock on failure
                cacheEntry.LocalLock.Release();
                throw;
            }
        }

        // Releases the write lock for the specified entry and returns the updated entry.
        // If the current session does not own the write-lock for the entry (f.e. if it is deleted), 
        // this method only releases the local lock but is a no-op otherwise.
        public Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            return ReleaseWriteLockAsync(entry).WithCancellation(cancellation);
        }

        public async Task<IStoredEntry> AcquireReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            // Enter local write lock, as we are modifying the stored entry with entering the read-lock
            await AcquireLocalLockAsync(path, cancellation);
            try
            {
                // Freshly load the entry from the database.
                var entry = await _storage.GetEntryAsync(path, cancellation);

                // Enter global read lock.
                return await InternalAcquireReadLockAsync(entry, cancellation);
            }
            finally
            {
                await ReleaseLocalLockAsync(path, cancellation);
            }
        }

        public async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var path = entry.Path;

            // Enter local write lock, as we are modifying the stored entry with entering the read-lock
            await AcquireLocalLockAsync(path, cancellation);
            try
            {
                // Freshly load the entry from the database.
                //var entry = await _storage.GetEntryAsync(path, cancellation);

                // Enter global read lock.
                return await InternalAcquireReadLockAsync(entry, cancellation);
            }
            finally
            {
                await ReleaseLocalLockAsync(path, cancellation);
            }
        }

        public Task<IStoredEntry> ReleaseReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            return ReleaseReadLockAsync(path).WithCancellation(cancellation);
        }

        #endregion

        private async Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry)
        {
            Assert(entry != null);

            try
            {
                var result = await InternalReleaseWriteLockAsync(entry);
                await ReleaseLocalLockAsync(entry.Path, cancellation: default);
                return result;
            }
            catch (SessionTerminatedException) { throw; }
            catch
            {
                _coordinationManager.Dispose();
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

            var session = await _coordinationManager.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Acquiring write-lock for entry '{entry.Path}'.");
            }

            // TODO: Check whether session is still alive

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

                Assert(start.WriteLock == null);

                // Actually try to lock the entry.
                // Do not use UpdateEntryAsync as this method assumes that we already own the write-lock.
                desired = _storedEntryManager.AcquireWriteLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (entry != start);

            // If we reached this point, we own the write lock.
            entry = desired;
            Assert(entry != null);

            try
            {
                _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Pending write-lock for entry '{entry.Path}'. Waiting for read-locks to release.");

                // Wait till all read-locks are freed.
                entry = await _waitManager.WaitForReadLocksReleaseAsync(entry, cancellation);

                // We hold the write lock. No-one can alter the entry except our session is terminated. But this will cause WaitForReadLocksReleaseAsync to throw.
                Assert(entry != null);

                // We own the write-lock.
                // All read-locks must be free except for ourself.
                Assert(entry.WriteLock == session);
                Assert(entry.ReadLocks.IsEmpty || entry.ReadLocks.Length == 1 && entry.ReadLocks[0] == session);

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    Assert(watch != null);
                    watch.Stop();

                    _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Acquired write-lock for entry '{entry.Path}' in {watch.Elapsed.TotalSeconds}sec.");
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
                    _coordinationManager.Dispose();
                    throw;
                }

                throw;
            }
        }

        private async Task<IStoredEntry> InternalReleaseWriteLockAsync(IStoredEntry entry)
        {
            var cancellation = CancellationToken.None; // TODO _coordinationManagerXX._disposeHelper.DisposalRequested;

            IStoredEntry start, desired;

            var session = await _coordinationManager.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Releasing write-lock for entry '{entry.Path}'.");
            }

            CacheEntry cacheEntry;

            do
            {
                start = entry;

                // The entry was deleted.
                if (start == null)
                {
                    _exchangeManager.NotifyWriteLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
                    _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Released write-lock for entry '{entry.Path}'.");
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
                cacheEntry = _cache.GetEntry(entry.Path);
                desired = _storedEntryManager.ReleaseWriteLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (entry != start);

            entry = desired;
            Assert(entry != null);
            _cache.UpdateEntry(cacheEntry, entry);

            if (entry != null)
            {
                _exchangeManager.NotifyWriteLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
                _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Released write-lock for entry '{entry.Path}'.");
            }

            return desired;
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

            var session = await _coordinationManager.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Acquiring read-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = await _waitManager.WaitForWriteLockReleaseAsync(entry, allowWriteLock: true, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Assert(start.WriteLock == null || start.WriteLock == session);

                desired = _storedEntryManager.AcquireReadLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);

            entry = desired;
            Assert(entry != null);
            Assert(entry.ReadLocks.Contains(session));

            try
            {
                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    Assert(watch != null);
                    watch.Stop();

                    _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Acquired read-lock for entry '{entry.Path.EscapedPath}' in {watch.ElapsedMilliseconds}ms.");
                }

                return entry;
            }
            catch
            {
                Assert(entry != null);

                // Release global read lock on failure.
                await ReleaseReadLockAsync(entry.Path);
                throw;
            }
        }

        private async Task<IStoredEntry> ReleaseReadLockAsync(CoordinationEntryPath path)
        {
            try
            {
                // Enter local write lock, as we are modifying the stored entry with entering the read-lock
                await AcquireLocalLockAsync(path, cancellation: default);
                try
                {
                    // Freshly load the entry from the database.
                    var entry = await _storage.GetEntryAsync(path, cancellation: default);

                    // Release global read lock.
                    return await InternalReleaseReadLockAsync(entry);
                }
                finally
                {
                    await ReleaseLocalLockAsync(path, cancellation: default);
                }
            }
            catch
            {
                _coordinationManager.Dispose();
                throw;
            }
        }

        private async Task<IStoredEntry> InternalReleaseReadLockAsync(IStoredEntry entry)
        {
            Assert(entry != null);

            var cancellation = CancellationToken.None; // TODO _coordinationManagerXX._disposeHelper.DisposalRequested;

            IStoredEntry start, desired;

            var session = await _coordinationManager.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Releasing read-lock for entry '{entry.Path}'.");
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

            Assert(entry != null);

            _exchangeManager.NotifyReadLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
            _logger?.LogTrace($"[{await _coordinationManager.GetSessionAsync(cancellation)}] Released read-lock for entry '{entry.Path}'.");

            return desired;
        }
    }
}
