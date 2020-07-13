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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Coordination.Session;
using AI4E.Utils;
using Nito.AsyncEx;

namespace AI4E.Storage.Coordination.Mocks
{
    public sealed class SessionManagerMock : ISessionManager
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly object _mutex = new object();
        private readonly ConcurrentDictionary<SessionIdentifier, SessionEntry> _sessions = new ConcurrentDictionary<SessionIdentifier, SessionEntry>();
        private readonly AsyncAutoResetEvent _sessionAddedEvent = new AsyncAutoResetEvent();

        public SessionManagerMock(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));
            _dateTimeProvider = dateTimeProvider;
        }

        private sealed class SessionEntry
        {
            private readonly IDateTimeProvider _dateTimeProvider;
            private readonly TaskCompletionSource<object> _terminationSource;
            private readonly CancellationTokenSource _terminationCancellationSource;
            private readonly object _mutex = new object();

            private DateTime _leaseEnd;

            public SessionEntry(SessionIdentifier session, IDateTimeProvider dateTimeProvider, DateTime leaseEnd)
            {
                Session = session;
                _dateTimeProvider = dateTimeProvider;
                _leaseEnd = leaseEnd;
                _terminationSource = new TaskCompletionSource<object>();
                _terminationCancellationSource = new CancellationTokenSource();

                _ = OnLeaseEnded(leaseEnd, _terminationCancellationSource.Token);
            }

            public Task Termination => _terminationSource.Task;

            public bool IsTerminated => Termination.IsCompleted;

            public SessionIdentifier Session { get; }

            public bool TryUpdate(DateTime leaseEnd)
            {
                var now = _dateTimeProvider.GetCurrentTime();

                lock (_mutex)
                {
                    if (_leaseEnd <= now)
                    {
                        TerminateCore();
                        return false;
                    }

                    if (IsTerminated)
                    {
                        return false;
                    }

                    _leaseEnd = leaseEnd;
                }

                return true;
            }

            private void TerminateCore()
            {
                _terminationSource.TrySetResult(null);
                try
                {
                    _terminationCancellationSource.Cancel();
                }
                catch (ObjectDisposedException) { }
            }

            public void Terminate()
            {
                lock (_mutex)
                {
                    TerminateCore();
                }
            }

            private async Task OnLeaseEnded(DateTime leaseEnd, CancellationToken cancellation)
            {
                try
                {
                    var now = _dateTimeProvider.GetCurrentTime();
                    var timeToWait = leaseEnd - now;

                    do
                    {
                        do
                        {
                            await Task.Delay(timeToWait, cancellation);

                            lock (_mutex)
                            {
                                if (_leaseEnd != leaseEnd)
                                {
                                    leaseEnd = _leaseEnd;
                                }
                            }

                            now = _dateTimeProvider.GetCurrentTime();
                            timeToWait = leaseEnd - now;
                        }
                        while (timeToWait > TimeSpan.Zero);

                        lock (_mutex)
                        {
                            now = _dateTimeProvider.GetCurrentTime();
                            timeToWait = leaseEnd - now;

                            if (timeToWait <= TimeSpan.Zero)
                            {
                                TerminateCore();
                            }
                        }
                    }
                    while (true);
                }
                finally
                {
                    _terminationCancellationSource.Dispose();
                }
            }
        }

        public Task<bool> TryBeginSessionAsync(SessionIdentifier session, DateTime leaseEnd, CancellationToken cancellation = default)
        {
            var entry = new SessionEntry(session, _dateTimeProvider, leaseEnd);

            bool result;
            lock (_mutex)
            {
                result = _sessions.TryAdd(session, entry);

                if (result)
                {
                    _sessionAddedEvent.Set();
                }
            }

            if (!result)
            {
                entry.Terminate();
            }

            return Task.FromResult(result);
        }

        // TODO: Return Task<bool> / ValueTask<bool> ?
        public Task UpdateSessionAsync(SessionIdentifier session, DateTime leaseEnd, CancellationToken cancellation = default)
        {
            if (!_sessions.TryGetValue(session, out var entry))
            {
                throw new SessionTerminatedException();
            }

            var result = entry.TryUpdate(leaseEnd);

            if (!result)
            {
                throw new SessionTerminatedException();
            }

            return Task.CompletedTask;
        }

        // TODO: ENDSession / WaitForTERMINATION -- NAMING
        public Task EndSessionAsync(SessionIdentifier session, CancellationToken cancellation = default)
        {
            if (_sessions.TryGetValue(session, out var entry))
            {
                entry.Terminate();
            }

            return Task.CompletedTask;
        }

        public Task WaitForTerminationAsync(SessionIdentifier session, CancellationToken cancellation = default)
        {
            if (!_sessions.TryGetValue(session, out var entry))
            {
                return Task.CompletedTask;
            }

            return entry.Termination.WithCancellation(cancellation);
        }

        public async Task<SessionIdentifier> WaitForTerminationAsync(CancellationToken cancellation = default)
        {
            async Task<SessionIdentifier> GetTermination(SessionEntry entry)
            {
                await entry.Termination.WithCancellation(cancellation);
                return entry.Session;
            }

            Task completed;
            Task entryAddedTask;

            do
            {
                ICollection<SessionEntry> entries;

                lock (_mutex)
                {
                    entries = _sessions.Values;
                    entryAddedTask = _sessionAddedEvent.WaitAsync(cancellation);
                }

                var tasks = entries.Select(GetTermination);
                completed = await Task.WhenAny(((IEnumerable<Task>)tasks).Append(entryAddedTask));
            }
            while (completed == entryAddedTask);

            return await (Task<SessionIdentifier>)completed;
        }

        // TODO: ENDSession / WaitForTERMINATION / isALIVE -- NAMING
        // TODO: Return ValueTask<bool>
        public Task<bool> IsAliveAsync(SessionIdentifier session, CancellationToken cancellation = default)
        {
            if (!_sessions.TryGetValue(session, out var entry))
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(!entry.IsTerminated);
        }

        public IAsyncEnumerable<SessionIdentifier> GetSessionsAsync(CancellationToken cancellation = default)
        {
            return _sessions.Keys.ToAsyncEnumerable();
        }

        #region Entries

        public Task AddSessionEntryAsync(SessionIdentifier session, CoordinationEntryPath entryPath, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveSessionEntryAsync(SessionIdentifier session, CoordinationEntryPath entryPath, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<CoordinationEntryPath>> GetEntriesAsync(SessionIdentifier session, CancellationToken cancellation = default)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
