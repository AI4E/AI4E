using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;
using AsyncWaitDirectory = AI4E.Coordination.AsyncWaitDirectory<(AI4E.Coordination.Session session, AI4E.Coordination.CoordinationEntryPath path)>;

namespace AI4E.Coordination
{
    internal sealed class CoordinationWaitManager : ICoordinationWaitManager
    {
        #region Fields

        private static readonly TimeSpan _timeToWaitMin = new TimeSpan(200 * TimeSpan.TicksPerMillisecond);
        private static readonly TimeSpan _timeToWaitMax = new TimeSpan(12800 * TimeSpan.TicksPerMillisecond);

        private readonly IProvider<ICoordinationManager> _coordinationManager;
        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ISessionManager _sessionManager;
        private readonly ICoordinationExchangeManager _exchangeManager;
        private readonly ILogger<CoordinationWaitManager> _logger;

        private readonly AsyncWaitDirectory _readLockWaitDirectory;
        private readonly AsyncWaitDirectory _writeLockWaitDirectory;

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
                if (writeLock == session)
                {
                    Assert(allowWriteLock); // If this fails, we have overlapping writes, that should be prevented by the local write lock.

                    return entry;
                }

                var path = entry.Path;

                if (!await _sessionManager.IsAliveAsync((Session)writeLock, cancellation))
                {
                    entry = await CleanupLocksOnSessionTermination(entry, (Session)writeLock, cancellation);
                    continue;
                }

                bool IsLockReleased(IStoredEntry e)
                {
                    if (e.WriteLock == null)
                        return true;

                    if (e.WriteLock == session)
                    {
                        Assert(allowWriteLock);
                        return true;
                    }

                    return false;
                }

                entry = await WaitForLockReleaseCoreAsync(entry,
                                                          (Session)writeLock,
                                                          _writeLockWaitDirectory,
                                                          acquireLockRelease: null,
                                                          isLockReleased: IsLockReleased,
                                                          cancellation);
            }

            return null;
        }

        public async Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            IEnumerable<Session> readLocks = entry.ReadLocks;

            // Exclude our own read-lock (if present)
            var session = await CoordinationManager.GetSessionAsync(cancellation);

            Assert(entry.WriteLock == session);

            readLocks = readLocks.Where(readLock => readLock != session);

            if (readLocks.Any())
            {
                // Send a message to each of 'readLocks' to ask for lock release and await the end of the session or the lock release, whichever occurs first.
                var lockReleaseResults = await Task.WhenAll(readLocks.Select(readLock => WaitForReadLockRelease(entry, readLock, cancellation)));
                Assert(lockReleaseResults != null);
                Assert(lockReleaseResults.Count() == readLocks.Count());
                Assert(lockReleaseResults.Any());

                // We own the write-lock. Anyone deleted the entry concurrently => Our session must be terminated.
                if (lockReleaseResults.Any(p => p == null))
                {
                    throw new SessionTerminatedException();
                }

                entry = lockReleaseResults.OrderByDescending(p => p.StorageVersion).FirstOrDefault();

                Assert(entry != null);

                // We are holding the write-lock => The entry must not be deleted concurrently.
                if (entry.ReadLocks.Length != 0 && (entry.ReadLocks.Length > 1 || entry.ReadLocks.First() != session))
                {
#if DEBUG
                    entry = await _storage.GetEntryAsync(entry.Path, cancellation);

                    var x = entry.ReadLocks.Length != 0 && (entry.ReadLocks.Length > 1 || entry.ReadLocks.First() != session);

                    System.Diagnostics.Debugger.Break();
#endif
                    throw new SessionTerminatedException();
                }
            }

            Assert(entry != null);
            return entry;
        }

        #endregion

        private async Task<IStoredEntry> CleanupLocksOnSessionTermination(IStoredEntry entry, Session session, CancellationToken cancellation)
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

            IStoredEntry start,
                         desired;

            do
            {
                cancellation.ThrowIfCancellationRequested();

                start = entry;

                if (start == null)
                {
                    return null;
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
                    return entry;
                }

                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);

            entry = desired;
            Assert(!entry.ReadLocks.Contains(session));
            Assert(entry.WriteLock != session);

            return entry;
        }

        private async Task<IStoredEntry> WaitForReadLockRelease(IStoredEntry entry, Session readLock, CancellationToken cancellation)
        {
            Assert(entry != null);
            Assert(entry.ReadLocks.Contains(readLock));

            if (!await _sessionManager.IsAliveAsync(readLock, cancellation))
            {
                entry = await CleanupLocksOnSessionTermination(entry, readLock, cancellation);
                Assert(!entry.ReadLocks.Contains(readLock));
                return entry;
            }

            do
            {
                entry = await WaitForLockReleaseCoreAsync(entry,
                                                          readLock,
                                                          _readLockWaitDirectory,
                                                          AcquireReadLockReleaseAsync,
                                                          isLockReleased: e => !e.ReadLocks.Contains(readLock),
                                                          cancellation);
            }
            while (entry != null && entry.ReadLocks.Contains(readLock));
            // We have to check for the read lock to be released, 
            // because we may assume the release caused by a message from the session that owns the read-lock. 
            // This message can be delayed and not be a response to our invalidation request.

            Assert(!entry.ReadLocks.Contains(readLock));

            return entry;
        }

        private Task AcquireReadLockReleaseAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            return _exchangeManager.InvalidateCacheEntryAsync(path, session, cancellation);
        }

        private async Task<IStoredEntry> WaitForLockReleaseCoreAsync(IStoredEntry entry,
                                                                     Session session,
                                                                     AsyncWaitDirectory waitDirectory,
                                                                     Func<CoordinationEntryPath, Session, CancellationToken, Task> acquireLockRelease,
                                                                     Func<IStoredEntry, bool> isLockReleased,
                                                                     CancellationToken cancellation)
        {
            var timeToWait = _timeToWaitMin;
            var path = entry.Path;
            var sessionTermination = _sessionManager.WaitForTerminationAsync(session, cancellation);
            var releaseNotification = waitDirectory.WaitForNotificationAsync((session, path), cancellation);

            while (entry != null && !isLockReleased(entry))
            {
                acquireLockRelease?.Invoke(path, session, cancellation).HandleExceptions(_logger);
                cancellation.ThrowIfCancellationRequested();

                var delay = Task.Delay(timeToWait, cancellation);

                var completed = await Task.WhenAny(delay, releaseNotification, sessionTermination);

                if (completed == releaseNotification)
                {
                    return await _storage.GetEntryAsync(path, cancellation);
                }

                if (completed == sessionTermination)
                {
                    return await CleanupLocksOnSessionTermination(entry, session, cancellation);
                }

                Assert(completed == delay);

                timeToWait = timeToWait + timeToWait;

                if (timeToWait > _timeToWaitMax)
                {
                    timeToWait = _timeToWaitMax;
                }

                entry = await _storage.GetEntryAsync(path, cancellation);
            }

            return entry;
        }
    }
}
