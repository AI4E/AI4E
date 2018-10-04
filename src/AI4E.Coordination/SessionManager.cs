using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;

namespace AI4E.Coordination
{
    public sealed class SessionManager : ISessionManager
    {
        private readonly ISessionStorage _storage;
        private readonly IStoredSessionManager _storedSessionManager;
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly Dictionary<Session, Task> _sessionTerminationCache = new Dictionary<Session, Task>();

        public SessionManager(ISessionStorage storage,
                              IStoredSessionManager storedSessionManager,
                              IDateTimeProvider dateTimeProvider)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storedSessionManager == null)
                throw new ArgumentNullException(nameof(storedSessionManager));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _storage = storage;
            _storedSessionManager = storedSessionManager;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<bool> TryBeginSessionAsync(Session session, DateTime leaseEnd, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var newSession = _storedSessionManager.Begin(session, leaseEnd);

            var previousSession = await _storage.UpdateSessionAsync(newSession, null, cancellation);

            return previousSession == null;
        }

        public async Task UpdateSessionAsync(Session session, DateTime leaseEnd, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                     start,
                     desired;

            do
            {
                start = current;

                if (start == null || _storedSessionManager.IsEnded(start))
                {
                    throw new SessionTerminatedException(session);
                }

                desired = _storedSessionManager.UpdateLease(start, leaseEnd);

                current = await _storage.UpdateSessionAsync(desired, start, cancellation);
            }
            while (start != current);
        }

        public async Task EndSessionAsync(Session session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                     start,
                     desired;

            do
            {
                start = current;

                if (start == null)
                {
                    return;
                }

                desired = start.EntryPaths.Any() ? _storedSessionManager.End(start) : null;
                current = await _storage.UpdateSessionAsync(desired, start, cancellation);
            }
            while (start != current);
        }

        public Task WaitForTerminationAsync(Session session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            lock (_sessionTerminationCache)
            {
                if (_sessionTerminationCache.TryGetValue(session, out var task))
                {
                    if (task.IsCompleted)
                    {
                        _sessionTerminationCache.Remove(session);
                    }

                    return task;
                }
            }

            var internalCancellationSource = new CancellationTokenSource();
            var result = InternalWaitForTerminationAsync(session, internalCancellationSource.Token);

            // The session is already terminated.
            if (result.IsCompleted)
            {
                return result;
            }

            lock (_sessionTerminationCache)
            {
                if (_sessionTerminationCache.ContainsKey(session))
                {
                    internalCancellationSource.Cancel();

                    result = _sessionTerminationCache[session];
                }
                else
                {
                    _sessionTerminationCache.Add(session, result);
                }
            }

            if (cancellation.CanBeCanceled)
            {
                return result.WithCancellation(cancellation);
            }

            return result;
        }

        private async Task InternalWaitForTerminationAsync(Session session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var start = await _storage.GetSessionAsync(session, cancellation);

            while (start != null)
            {
                if (_storedSessionManager.IsEnded(start))
                {
                    lock (_sessionTerminationCache)
                    {
                        _sessionTerminationCache.Remove(session);
                    }

                    return;
                }

                var now = _dateTimeProvider.GetCurrentTime();
                var timeToWait = start.LeaseEnd - now;

                await Task.Delay(timeToWait, cancellation);

                var current = await _storage.GetSessionAsync(start.Session, cancellation);

                if (start != current)
                {
                    start = current;
                }
            }

            return;
        }

        public async Task<Session> WaitForTerminationAsync(CancellationToken cancellation)
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();

                var delay = TimeSpan.FromSeconds(2);
                var sessions = _storage.GetSessionsAsync(cancellation);

                var enumerator = sessions.GetEnumerator();

                try
                {
                    while (await enumerator.MoveNext(cancellation))
                    {
                        var session = enumerator.Current;

                        if (_storedSessionManager.IsEnded(session))
                            return session.Session;

                        var now = _dateTimeProvider.GetCurrentTime();
                        var timeToWait = session.LeaseEnd - now;

                        if (timeToWait < delay)
                            delay = timeToWait;
                    }
                }
                finally
                {
                    enumerator.Dispose();
                }

                await Task.Delay(delay, cancellation);
            }
        }

        public async Task<bool> IsAliveAsync(Session session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var s = await _storage.GetSessionAsync(session, cancellation);

            return s != null && !_storedSessionManager.IsEnded(s);
        }

        public async Task AddSessionEntryAsync(Session session, CoordinationEntryPath entryPath, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                           start,
                           desired;

            do
            {
                start = current;

                if (start == null || _storedSessionManager.IsEnded(start))
                {
                    throw new SessionTerminatedException();
                }

                desired = _storedSessionManager.AddEntry(start, entryPath);

                current = await _storage.UpdateSessionAsync(desired, start, cancellation);
            }
            while (start != current);
        }

        public async Task RemoveSessionEntryAsync(Session session, CoordinationEntryPath entryPath, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                     start,
                     desired;

            do
            {
                start = current;

                if (start == null)
                {
                    return;
                }

                desired = _storedSessionManager.RemoveEntry(start, entryPath);

                if (_storedSessionManager.IsEnded(desired) && !desired.EntryPaths.Any())
                {
                    desired = null;
                }

                current = await _storage.UpdateSessionAsync(desired, start, cancellation);
            }
            while (start != current);
        }

        public async Task<IEnumerable<CoordinationEntryPath>> GetEntriesAsync(Session session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var current = await _storage.GetSessionAsync(session, cancellation);

            if (current == null)
                return Enumerable.Empty<CoordinationEntryPath>();

            return current.EntryPaths;
        }

        public IAsyncEnumerable<Session> GetSessionsAsync(CancellationToken cancellation)
        {
            return _storage.GetSessionsAsync(cancellation)
                           .Where(p => !_storedSessionManager.IsEnded(p))
                           .Select(p => p.Session);
        }
    }
}
