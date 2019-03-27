/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Storage;
using AI4E.Utils;

namespace AI4E.Coordination.Session
{
    /// <summary>
    /// Manages coordination service sessions.
    /// </summary>
    public sealed class SessionManager : ISessionManager
    {
        private readonly ISessionStorage _storage;
        private readonly IStoredSessionManager _storedSessionManager;
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly Dictionary<CoordinationSession, Task> _sessionTerminationCache = new Dictionary<CoordinationSession, Task>();

        /// <summary>
        /// Creates a new instance of the <see cref="SessionManager"/> type.
        /// </summary>
        /// <param name="storage">A <see cref="ISessionStorage"/> that is used to store session entries.</param>
        /// <param name="storedSessionManager">A <see cref="IStoredSessionManager"/> that is used to create session entries.</param>
        /// <param name="dateTimeProvider">A <see cref="IDateTimeProvider"/> that is used to obtain the current date and time.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="storage"/>, <paramref name="storedSessionManager"/>
        /// or <paramref name="dateTimeProvider"/> is <c>null</c>.
        /// </exception>
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

        /// <inheritdoc/>
        public async Task AddSessionEntryAsync(CoordinationSession session, CoordinationEntryPath entryPath, CancellationToken cancellation)
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
            while (start != current && start.StorageVersion != current.StorageVersion);
        }

        /// <inheritdoc/>
        public async Task RemoveSessionEntryAsync(CoordinationSession session, CoordinationEntryPath entryPath, CancellationToken cancellation)
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
            while (start != current && start.StorageVersion != current.StorageVersion);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<CoordinationEntryPath>> GetEntriesAsync(CoordinationSession session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var current = await _storage.GetSessionAsync(session, cancellation);

            if (current == null)
                return Enumerable.Empty<CoordinationEntryPath>();

            return current.EntryPaths;
        }

        /// <inheritdoc/>
        public async Task<bool> TryBeginSessionAsync(CoordinationSession session, DateTime leaseEnd, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var newSession = _storedSessionManager.Begin(session, leaseEnd);

            var previousSession = await _storage.UpdateSessionAsync(newSession, null, cancellation);

            return previousSession == null;
        }

        /// <inheritdoc/>
        public async Task UpdateSessionAsync(CoordinationSession session, DateTime leaseEnd, CancellationToken cancellation)
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
            while (start != current && start.StorageVersion != current.StorageVersion);
        }

        /// <inheritdoc/>
        public async Task EndSessionAsync(CoordinationSession session, CancellationToken cancellation)
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

                desired = start.EntryPaths.IsDefaultOrEmpty ? null : _storedSessionManager.End(start);
                current = await _storage.UpdateSessionAsync(desired, start, cancellation);
            }
            while (start != current && start.StorageVersion != current.StorageVersion);
        }

        /// <inheritdoc/>
        public Task WaitForTerminationAsync(CoordinationSession session, CancellationToken cancellation)
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

        private async Task InternalWaitForTerminationAsync(CoordinationSession session, CancellationToken cancellation)
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

                if (start != current && start.StorageVersion != current.StorageVersion)
                {
                    start = current;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<CoordinationSession> WaitForTerminationAsync(CancellationToken cancellation)
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

        /// <inheritdoc/>
        public async Task<bool> IsAliveAsync(CoordinationSession session, CancellationToken cancellation)
        {
            if (session == default)
                throw new ArgumentDefaultException(nameof(session));

            var s = await _storage.GetSessionAsync(session, cancellation);

            return s != null && !_storedSessionManager.IsEnded(s);
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<CoordinationSession> GetSessionsAsync(CancellationToken cancellation)
        {
            return _storage.GetSessionsAsync(cancellation)
                           .Where(p => !_storedSessionManager.IsEnded(p))
                           .Select(p => p.Session);
        }
    }
}
