using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Coordination
{
    internal sealed class CoordinationWaitManager : ICoordinationWaitManager
    {
        #region Fields

        private readonly IProvider<ICoordinationManager> _coordinationManager;
        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ISessionManager _sessionManager;
        private readonly ICoordinationExchangeManager _exchangeManager;
        private readonly ILogger<CoordinationWaitManager> _logger;

        private readonly AsyncWaitDirectory<(Session session, CoordinationEntryPath path)> _readLockWaitDirectory;
        private readonly AsyncWaitDirectory<(Session session, CoordinationEntryPath path)> _writeLockWaitDirectory;

        #endregion

        #region C'tor

        public CoordinationWaitManager(IProvider<ICoordinationManager> coordinationManager,
                                       ICoordinationStorage storage,
                                       IStoredEntryManager storedEntryManager,
                                       ISessionManager sessionManager,
                                       ICoordinationExchangeManager exchangeManager,
                                       ILogger<CoordinationWaitManager> logger = null)
        {
            if (coordinationManager == null)
                throw new ArgumentNullException(nameof(coordinationManager));

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (exchangeManager == null)
                throw new ArgumentNullException(nameof(exchangeManager));

            _coordinationManager = coordinationManager;
            _storage = storage;
            _storedEntryManager = storedEntryManager;
            _sessionManager = sessionManager;
            _exchangeManager = exchangeManager;
            _logger = logger;

            _readLockWaitDirectory = new AsyncWaitDirectory<(Session session, CoordinationEntryPath path)>();
            _writeLockWaitDirectory = new AsyncWaitDirectory<(Session session, CoordinationEntryPath path)>();
        }

        #endregion

        public ICoordinationManager CoordinationManager => _coordinationManager.ProvideInstance();

        #region ICoordinationWaitManager

        public void NotifyReadLockRelease(CoordinationEntryPath path, Session session)
        {
            _readLockWaitDirectory.Notify((session, path));
        }

        public void NotifyWriteLockRelease(CoordinationEntryPath path, Session session)
        {
            _writeLockWaitDirectory.Notify((session, path));
        }

        // WaitForWriteLockReleaseAsync updates the session in order that there is enough time
        // to complete the write operation, without the session to terminate.
        public async Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, bool allowWriteLock, CancellationToken cancellation)
        {
            var session = await CoordinationManager.GetSessionAsync(cancellation);

            // The entry was deleted (concurrently).
            while (entry != null)
            {
                var writeLock = entry.WriteLock;

                if (writeLock == null)
                {
                    return entry;
                }

                // If (entry.WriteLock == session) we MUST wait till the lock is released
                // and acquired again in order that no concurrency conflicts may occur.
                // Because of the case that we may keep a write and read lock at the same time, this does hold true only if we aquire a write lock.
                // In this case, we could also allow this but had to synchronize the write operations locally.
                if (allowWriteLock && writeLock == session)
                {
                    return entry;
                }

                var path = entry.Path;

                async Task<bool> Predicate(CancellationToken c)
                {
                    entry = await _storage.GetEntryAsync(path, c);
                    return entry == null || entry.WriteLock == null;
                }

                Task Release(CancellationToken c)
                {
                    return _writeLockWaitDirectory.WaitForNotificationAsync(((Session)writeLock, path), c);
                }

                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

                try

                {
                    var lockRelease = SpinWaitAsync(Predicate, Release, combinedCancellationSource.Token);
                    var sessionTermination = _sessionManager.WaitForTerminationAsync((Session)writeLock);
                    var completed = await (Task.WhenAny(sessionTermination, lockRelease).WithCancellation(cancellation));

                    if (completed == sessionTermination)
                    {
                        await CleanupLocksOnSessionTermination(path, (Session)writeLock, cancellation);
                        entry = await _storage.GetEntryAsync(path, cancellation);
                    }
                }
                finally
                {
                    combinedCancellationSource.Cancel();
                }
            }

            return null;
        }

        public async Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            IEnumerable<Session> readLocks = entry.ReadLocks;

            // Exclude our own read-lock (if present)
            var session = await CoordinationManager.GetSessionAsync(cancellation);
            readLocks = readLocks.Where(readLock => readLock != session);

            // Send a message to each of 'readLocks' to ask for lock release and await the end of the session or the lock release, whichever occurs first.
            await Task.WhenAll(readLocks.Select(readLock => WaitForReadLockRelease(entry.Path, readLock, cancellation)));

            // The unlock of the 'readLocks' will alter the db, so we have to read the entry again.
            entry = await _storage.GetEntryAsync(entry.Path, cancellation);

            // We are holding the write-lock => The entry must not be deleted concurrently.
            if (entry == null || entry.ReadLocks.Length != 0 && (entry.ReadLocks.Length > 1 || entry.ReadLocks.First() != session))
            {
                throw new SessionTerminatedException();
            }

            return entry;
        }

        #endregion

        private async Task CleanupLocksOnSessionTermination(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
#if DEBUG
            var isTerminated = !await _sessionManager.IsAliveAsync(session, cancellation);

            Assert(isTerminated);
#endif

            var localSession = await CoordinationManager.GetSessionAsync(cancellation);

            // We waited for ourself to terminate => We are terminated now.
            if (session == localSession)
            {
                throw new SessionTerminatedException();
            }

            IStoredEntry entry = await _storage.GetEntryAsync(path, cancellation),
                         start,
                         desired;

            do
            {
                cancellation.ThrowIfCancellationRequested();

                start = entry;

                if (start == null)
                {
                    return;
                }

                desired = start;

                if (entry.WriteLock == session)
                {
                    desired = _storedEntryManager.ReleaseWriteLock(desired, session);
                }

                if (entry.ReadLocks.Contains(session))
                {
                    desired = _storedEntryManager.ReleaseReadLock(desired, session);
                }

                if (StoredEntryUtil.AreVersionEqual(start, desired))
                {
                    return;
                }

                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);
        }

        private async Task WaitForReadLockRelease(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            if (entry == null || !entry.ReadLocks.Contains(session))
            {
                return;
            }

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
            {
                await CleanupLocksOnSessionTermination(path, session, cancellation);
                return;
            }

            async Task<bool> Predicate(CancellationToken c)
            {
                entry = await _storage.GetEntryAsync(path, c);

                if (entry != null && entry.ReadLocks.Contains(session))
                {
                    _exchangeManager.InvalidateCacheEntryAsync(path, session, c).HandleExceptions(_logger);
                    return false;
                }

                return true;
            }

            Task Release(CancellationToken c)
            {
                return _readLockWaitDirectory.WaitForNotificationAsync((session, path), c);
            }

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            try
            {
                var lockRelease = SpinWaitAsync(Predicate, Release, combinedCancellationSource.Token);
                var sessionTermination = _sessionManager.WaitForTerminationAsync(session);
                var completed = await (Task.WhenAny(sessionTermination, lockRelease).WithCancellation(cancellation));

                if (completed == sessionTermination)
                {
                    await CleanupLocksOnSessionTermination(path, session, cancellation);
                    entry = await _storage.GetEntryAsync(path, cancellation);
                }
            }
            finally
            {
                combinedCancellationSource.Cancel();
            }

        }

        private async Task SpinWaitAsync(Func<CancellationToken, Task<bool>> predicate, Func<CancellationToken, Task> release, CancellationToken cancellation)
        {
            var timeToWait = new TimeSpan(200 * TimeSpan.TicksPerMillisecond);
            var timeToWaitMax = new TimeSpan(12800 * TimeSpan.TicksPerMillisecond);

            cancellation.ThrowIfCancellationRequested();
            var releaseX = release(cancellation); // TODO: Rename

            while (!await predicate(cancellation))
            {
                cancellation.ThrowIfCancellationRequested();

                var delay = Task.Delay(timeToWait, cancellation);

                var completed = await Task.WhenAny(delay, releaseX);

                if (completed == releaseX)
                {
                    return;
                }

                if (timeToWait < timeToWaitMax)
                    timeToWait = new TimeSpan(timeToWait.Ticks * 2);
            }
        }
    }
}
