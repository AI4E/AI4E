using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;
using static AI4E.Utils.DebugEx;

namespace AI4E.Coordination
{
    internal sealed class CoordinationLockManager : ICoordinationLockManager
    {
        #region Fields

        private readonly ICoordinationSessionOwner _sessionOwner;
        private readonly ISessionManager _sessionManager;
        //private readonly CoordinationEntryCache _cache;
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
                                       //CoordinationEntryCache cache,
                                       ICoordinationStorage storage,
                                       IStoredEntryManager storedEntryManager,
                                       ICoordinationWaitManager waitManager,
                                       ICoordinationExchangeManager exchangeManager,
                                       ILogger<CoordinationLockManager> logger = null)
        {
            //if (cache == null)
            //    throw new ArgumentNullException(nameof(cache));

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
            //_cache = cache;
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


        // Acquired a read lock for the entry with the specified path and returns the entry.
        // If the result is null, the entry does not exist and no lock is allocated.
        public async Task<IStoredEntry> AcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

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
                _logger?.LogTrace($"[{session}] Pending write-lock for entry '{entry.Path}'. Waiting for read-locks to release.");

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

                    _logger?.LogTrace($"[{session}] Acquired write-lock for entry '{entry.Path}' in {watch.Elapsed.TotalSeconds}sec.");
                }

                return entry;
            }
            catch
            {
                try
                {
                    await ReleaseWriteLockAsync(entry);
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

        // Releases the write lock for the specified entry and returns the updated entry.
        // If the current session does not own the write-lock for the entry (f.e. if it is deleted), 
        // this method only releases the local lock but is a no-op otherwise.
        public async Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            Assert(entry != null);

            var path = entry.Path;
            var cancellation = (await _sessionTerminationSource).Token;
            IStoredEntry start, desired;

            var session = await _sessionOwner.GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{session}] Releasing write-lock for entry '{path}'.");
            }

            do
            {
                start = entry;

                // The entry was deleted.
                if (start == null)
                {
                    _exchangeManager.NotifyWriteLockReleasedAsync(path, cancellation).HandleExceptions(_logger);
                    _logger?.LogTrace($"[{session}] Released write-lock for entry '{path}'.");
                    return start;
                }

                // The session does not own the write lock.
                if (start.WriteLock != session)
                {
                    return start;
                }

                desired = _storedEntryManager.ReleaseWriteLock(start, session);
                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (entry != start);

            entry = desired;
            Assert(entry != null);
            Assert(entry.WriteLock == null);

            if (entry != null)
            {
                _exchangeManager.NotifyWriteLockReleasedAsync(path, cancellation).HandleExceptions(_logger);
                _logger?.LogTrace($"[{session}] Released write-lock for entry '{path}'.");
            }

            return entry;

        }

        public async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

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

                    _logger?.LogTrace($"[{session}] Acquired read-lock for entry '{entry.Path.EscapedPath}' in {watch.ElapsedMilliseconds}ms.");
                }

                return entry;
            }
            catch
            {
                Assert(entry != null);

                // Release global read lock on failure.
                await ReleaseReadLockAsync(entry);
                throw;
            }
        }

        public async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            Assert(entry != null);

            var cancellation = (await _sessionTerminationSource).Token;
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

            Assert(entry != null);

            _exchangeManager.NotifyReadLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
            _logger?.LogTrace($"[{session}] Released read-lock for entry '{entry.Path}'.");

            return desired;
        }

        #endregion
    }
}
