using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed partial class InMemoryCoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly Dictionary<string, IStoredEntry> _entries = new Dictionary<string, IStoredEntry>();
        private readonly Dictionary<string, IStoredSession> _sessions = new Dictionary<string, IStoredSession>();
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly IStoredSessionManager _storedSessionManager;

        public InMemoryCoordinationStorage(IDateTimeProvider dateTimeProvider, IStoredEntryManager storedEntryManager, IStoredSessionManager storedSessionManager)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            if (storedSessionManager == null)
                throw new ArgumentNullException(nameof(storedSessionManager));

            _dateTimeProvider = dateTimeProvider;
            _storedEntryManager = storedEntryManager;
            _storedSessionManager = storedSessionManager;
        }

        #region Entry

        public IStoredEntry CreateEntry(string path, string session, bool isEphemeral, byte[] value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return _storedEntryManager.Create(path, session, isEphemeral, value.ToImmutableArray());
        }

        public Task<IStoredEntry> UpdateEntryAsync(IStoredEntry comparand, IStoredEntry value, CancellationToken cancellation)
        {
            string path;

            if (comparand != null && value != null)
            {
                if (comparand.Path != value.Path)
                {
                    throw new ArgumentException("The path of the comparand must be equal to the path of the new value.");
                }

                if (value.StorageVersion == comparand.StorageVersion)
                {
                    return Task.FromResult(value);
                }

                path = comparand.Path;
            }
            else if (comparand != null)
            {
                path = comparand.Path;
            }
            else if (value != null)
            {
                path = value.Path;
            }
            else // (value == null && comparand == null)
            {
                throw new ArgumentException("Either comparand or value may be null but not both.");
            }

            var convertedValue = _storedEntryManager.Copy(value);
            var comparandVersion = comparand?.StorageVersion ?? 0;

            lock (_entries)
            {
                return CompareExchange(path, convertedValue, comparandVersion);
            }
        }

        private Task<IStoredEntry> CompareExchange(string path, IStoredEntry value, int comparandVersion)
        {
            var currentVersion = 0;

            if (_entries.TryGetValue(path, out var currentEntry))
            {
                Assert(currentEntry != null);

                currentVersion = currentEntry.StorageVersion;
            }
            else
            {
                currentEntry = null;
            }

            if (comparandVersion == currentVersion)
            {
                if (value == null)
                {
                    _entries.Remove(path);
                }
                else
                {
                    _entries[path] = value;
                }
            }

            return Task.FromResult(currentEntry);
        }

        public Task<IStoredEntry> GetEntryAsync(string path, CancellationToken cancellation)
        {
            var entry = default(IStoredEntry);

            lock (_entries)
            {
                if (!_entries.TryGetValue(path, out entry))
                {
                    return Task.FromResult(default(IStoredEntry));
                }
            }

            Assert(entry != null);
            Assert(entry.Path == path);

            return Task.FromResult(entry);
        }

        #endregion

        #region Session

        public IStoredSession CreateSession(string key, DateTime leaseEnd)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return _storedSessionManager.Begin(key, leaseEnd);
        }

        public Task<IStoredSession> UpdateSessionAsync(IStoredSession comparand, IStoredSession value, CancellationToken cancellation)
        {
            string key;

            if (comparand != null && value != null)
            {
                if (comparand.Key != value.Key)
                {
                    throw new ArgumentException("The key of the comparand must be equal to the key of the new value.");
                }

                if (value.StorageVersion == comparand.StorageVersion)
                {
                    return Task.FromResult(value);
                }

                key = comparand.Key;
            }
            else if (comparand != null)
            {
                key = comparand.Key;
            }
            else if (value != null)
            {
                key = value.Key;
            }
            else // (value == null && comparand == null)
            {
                throw new ArgumentException("Either comparand or value may be null but not both.");
            }

            var convertedValue = _storedSessionManager.Copy(value);
            var comparandVersion = comparand?.StorageVersion ?? 0;

            lock (_sessions)
            {
                return CompareExchange(key, convertedValue, comparandVersion);
            }
        }


        private Task<IStoredSession> CompareExchange(string key, IStoredSession value, int comparandVersion)
        {
            var currentVersion = 0;

            if (_sessions.TryGetValue(key, out var currentSession))
            {
                Assert(currentSession != null);

                currentVersion = currentSession.StorageVersion;
            }
            else
            {
                currentSession = null;
            }

            if (comparandVersion == currentVersion)
            {
                if (value == null)
                {
                    _sessions.Remove(key);
                }
                else
                {
                    _sessions[key] = value;
                }
            }

            return Task.FromResult<IStoredSession>(currentSession);
        }

        public Task<IStoredSession> GetSessionAsync(string key, CancellationToken cancellation)
        {
            var session = default(IStoredSession);

            lock (_sessions)
            {
                if (!_sessions.TryGetValue(key, out session))
                {
                    return Task.FromResult<IStoredSession>(null);
                }
            }

            Assert(session != null);
            Assert(session.Key == key);

            return Task.FromResult(session);
        }

        public Task<IEnumerable<IStoredSession>> GetSessionsAsync(CancellationToken cancellation)
        {
            lock (_sessions)
            {
                return Task.FromResult<IEnumerable<IStoredSession>>(_sessions.Values.ToArray());
            }
        }

        #endregion
    }
}
