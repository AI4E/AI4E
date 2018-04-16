using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    // TODO: Sessions are never actually deleted currently.
    public sealed class SessionManager : ISessionManager
    {
        private readonly ISessionStorage _storage;

        public SessionManager(ISessionStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            _storage = storage;
        }

        public async Task<bool> TryBeginSessionAsync(string session, CancellationToken cancellation = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var newSession = _storage.CreateSession(session);

            var previousSession = await _storage.UpdateSessionAsync(null, newSession, cancellation);

            return previousSession == null;
        }

        public async Task UpdateSessionAsync(string session, DateTime leaseEnd, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                     start,
                     desired;

            do
            {
                start = current;

                if (start == null || start.IsEnded)
                {
                    throw new SessionTerminatedException(session);
                }

                desired = start.UpdateLease(leaseEnd);

                current = await _storage.UpdateSessionAsync(start, desired, cancellation);
            }
            while (start != current);
        }

        public async Task EndSessionAsync(string session, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                     start,
                     desired;

            do
            {
                start = current;

                if (start == null || start.IsEnded)
                {
                    return;
                }

                desired = start.End();

                current = await _storage.UpdateSessionAsync(start, desired, cancellation);
            }
            while (start != current);
        }

        public async Task WaitForTerminationAsync(string session, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var start = await _storage.GetSessionAsync(session, cancellation);

            if (start != null)
            {
                await InternalWaitForTerminationAsync(start, cancellation);
            }
        }

        private async Task<IStoredSession> InternalWaitForTerminationAsync(IStoredSession session, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            do
            {
                if (session.IsEnded)
                    return session;

                var now = DateTime.Now;
                var timeToWait = session.LeaseEnd - now;

                await Task.Delay(timeToWait, cancellation);

                var current = await _storage.GetSessionAsync(session.Key, cancellation);

                if (session != current)
                {
                    session = current;
                }
            }
            while (session != null);

            return session;
        }

        public async Task<string> WaitForTerminationAsync(CancellationToken cancellation)
        {
            var sessions = await _storage.GetSessionsAsync(cancellation);

            if (!sessions.Any())
            {
                return null;
            }

            Assert(sessions.All(p => p != null));

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            try
            {
                var completed = await Task.WhenAny(sessions.Select(p => InternalWaitForTerminationAsync(p, combinedCancellationSource.Token)));

                return (await completed).Key;
            }
            finally
            {
                combinedCancellationSource.Cancel();
            }
        }

        public async Task<bool> IsAliveAsync(string session, CancellationToken cancellation = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var s = await _storage.GetSessionAsync(session, cancellation);

            return s == null || s.IsEnded;
        }

        public async Task AddSessionEntryAsync(string session, string entry, CancellationToken cancellation = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            IStoredSession current = await _storage.GetSessionAsync(session, cancellation),
                     start,
                     desired;

            do
            {
                start = current;

                if (start == null || start.IsEnded)
                {
                    throw new SessionTerminatedException();
                }

                desired = start.AddEntry(entry);

                current = await _storage.UpdateSessionAsync(start, desired, cancellation);
            }
            while (start != current);
        }

        public async Task RemoveSessionEntryAsync(string session, string entry, CancellationToken cancellation = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

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

                desired = start.RemoveEntry(entry);

                if (desired.IsEnded && !desired.Entries.Any())
                {
                    desired = null;
                }

                current = await _storage.UpdateSessionAsync(start, desired, cancellation);
            }
            while (start != current);
        }

        public async Task<IEnumerable<string>> GetEntriesAsync(string session, CancellationToken cancellation = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var current = await _storage.GetSessionAsync(session, cancellation);

            if (current == null)
                return Enumerable.Empty<string>();

            return current.Entries;
        }
    }
}
