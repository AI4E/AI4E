using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class InMemoryCoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();

        public InMemoryCoordinationStorage() { }

        #region Entry

        public IStoredEntry CreateEntry(string path, string session, bool isEphemeral, byte[] value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new Entry(path, session, isEphemeral, value.ToImmutableArray());
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

            var convertedValue = ConvertValue(value);
            var comparandVersion = comparand?.StorageVersion ?? 0;

            lock (_entries)
            {
                return CompareExchange(path, convertedValue, comparandVersion);
            }
        }

        private Task<IStoredEntry> CompareExchange(string path, Entry value, int comparandVersion)
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

            return Task.FromResult<IStoredEntry>(currentEntry);
        }

        private static Entry ConvertValue(IStoredEntry value)
        {
            Entry convertedValue = null;

            if (value != null)
            {
                convertedValue = value as Entry ?? new Entry(value);

                Assert(convertedValue != null);
                Assert(convertedValue.Path == value.Path);
            }

            return convertedValue;
        }

        public Task<IStoredEntry> GetEntryAsync(string path, CancellationToken cancellation)
        {
            var entry = default(Entry);

            lock (_entries)
            {
                if (!_entries.TryGetValue(path, out entry))
                {
                    return Task.FromResult<IStoredEntry>(null);
                }
            }

            Assert(entry != null);
            Assert(entry.Path == path);

            return Task.FromResult<IStoredEntry>(entry);
        }

        private sealed class Entry : IStoredEntry
        {
            private static readonly ImmutableArray<string> _noReadLocks = ImmutableArray<string>.Empty;

            #region C'tor

            public Entry(string key, string session, bool isEphemeral, ImmutableArray<byte> value)
            {
                Path = key;
                Value = value;
                WriteLock = session;
                ReadLocks = _noReadLocks;
                Childs = ImmutableArray<string>.Empty;
                LastWriteTime = CreationTime = DateTime.Now;
                Version = 1;
                StorageVersion = 1;

                if (isEphemeral)
                {
                    EphemeralOwner = session;
                }
            }

            public Entry(IStoredEntry entry)
            {
                Path = entry.Path;
                Value = entry.Value;
                ReadLocks = entry.ReadLocks;
                WriteLock = entry.WriteLock;
                Childs = entry.Childs;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Version = entry.Version;
                StorageVersion = entry.StorageVersion;
                EphemeralOwner = entry.EphemeralOwner;
            }

            private Entry(string key,
                          ImmutableArray<byte> value,
                          ImmutableArray<string> readLocks,
                          string writeLock,
                          ImmutableArray<string> childs,
                          int version,
                          int storageVersion,
                          string ephemeralOwner,
                          DateTime creationTime)
            {
                Path = key;
                Value = value;
                ReadLocks = readLocks;
                WriteLock = writeLock;
                Childs = childs;
                Version = version;
                StorageVersion = storageVersion;
                EphemeralOwner = ephemeralOwner;
                CreationTime = creationTime;
                LastWriteTime = DateTime.Now;
            }

            private Entry(string key,
                          ImmutableArray<byte> value,
                          ImmutableArray<string> readLocks,
                          string writeLock,
                          ImmutableArray<string> childs,
                          int version,
                          int storageVersion,
                          string ephemeralOwner,
                          DateTime creationTime,
                          DateTime lastWriteTime)
            {
                Path = key;
                Value = value;
                ReadLocks = readLocks;
                WriteLock = writeLock;
                Childs = childs;
                Version = version;
                StorageVersion = storageVersion;
                EphemeralOwner = ephemeralOwner;
                CreationTime = creationTime;
                LastWriteTime = lastWriteTime;
            }

            #endregion

            #region Properties

            public string Path { get; }

            public ImmutableArray<byte> Value { get; }

            public ImmutableArray<string> ReadLocks { get; }

            public string WriteLock { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public ImmutableArray<string> Childs { get; }

            public int Version { get; }

            public string EphemeralOwner { get; }

            public int StorageVersion { get; }

            #endregion

            public IStoredEntry AcquireWriteLock(string session)
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                if (WriteLock == session)
                    return this;

                if (WriteLock != null)
                    throw new InvalidOperationException();

                return new Entry(Path, Value, ReadLocks, writeLock: session, Childs, Version, StorageVersion + 1, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public IStoredEntry ReleaseWriteLock()
            {
                if (WriteLock == null)
                    return null;

                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                return new Entry(Path, Value, _noReadLocks, writeLock: null, Childs, Version, StorageVersion + 1, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public IStoredEntry AcquireReadLock(string session)
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                if (WriteLock != null)
                    throw new InvalidOperationException();

                if (ReadLocks.Contains(session))
                    return this;

                return new Entry(Path, Value, ReadLocks.Add(session), writeLock: null, Childs, Version, StorageVersion + 1, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public IStoredEntry ReleaseReadLock(string session)
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                if (ReadLocks.Length == 0 || !ReadLocks.Contains(session))
                    return this;

                return new Entry(Path, Value, ReadLocks.Remove(session), WriteLock, Childs, Version, StorageVersion + 1, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public IStoredEntry Remove()
            {
                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                if (Childs.Any())
                    throw new InvalidOperationException();

                return null;
            }

            public IStoredEntry SetValue(ImmutableArray<byte> value)
            {
                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                return new Entry(Path, value, _noReadLocks, WriteLock, Childs, Version + 1, StorageVersion + 1, EphemeralOwner, CreationTime);
            }

            public IStoredEntry AddChild(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));

                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                if (Childs.Contains(name))
                    return this;

                return new Entry(Path, Value, ReadLocks, WriteLock, Childs.Add(name), Version, StorageVersion + 1, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public IStoredEntry RemoveChild(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));

                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                if (!Childs.Contains(name))
                    return this;

                return new Entry(Path, Value, ReadLocks, WriteLock, Childs.Remove(name), Version, StorageVersion + 1, EphemeralOwner, CreationTime, LastWriteTime);
            }
        }

        #endregion

        #region Session

        public IStoredSession CreateSession(string key, DateTime leaseEnd)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return new Session(key, leaseEnd);
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

            var convertedValue = ConvertValue(value);
            var comparandVersion = comparand?.StorageVersion ?? 0;

            lock (_sessions)
            {
                return CompareExchange(key, convertedValue, comparandVersion);
            }
        }

        private Task<IStoredSession> CompareExchange(string key, Session value, int comparandVersion)
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

        private static Session ConvertValue(IStoredSession value)
        {
            Session convertedValue = null;

            if (value != null)
            {
                convertedValue = value as Session ?? new Session(value);

                Assert(convertedValue != null);
                Assert(convertedValue.Key == value.Key);
            }

            return convertedValue;
        }

        public Task<IStoredSession> GetSessionAsync(string key, CancellationToken cancellation)
        {
            var session = default(Session);

            lock (_sessions)
            {
                if (!_sessions.TryGetValue(key, out session))
                {
                    return Task.FromResult<IStoredSession>(null);
                }
            }

            Assert(session != null);
            Assert(session.Key == key);

            return Task.FromResult<IStoredSession>(session);
        }

        public Task<IEnumerable<IStoredSession>> GetSessionsAsync(CancellationToken cancellation)
        {
            lock (_sessions)
            {
                return Task.FromResult<IEnumerable<IStoredSession>>(_sessions.Values.ToArray());
            }
        }

        private sealed class Session : IStoredSession
        {
            private readonly bool _isEnded;

            public Session(string key, DateTime leaseEnd)
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                Key = key;
                LeaseEnd = leaseEnd;
                _isEnded = false;
                Entries = ImmutableArray<string>.Empty;
                StorageVersion = 1;
            }

            public Session(IStoredSession session)
            {
                Key = session.Key;
                _isEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                Entries = session.Entries;
                StorageVersion = session.StorageVersion;
            }

            private Session(string key, bool isEnded, DateTime leaseEnd, ImmutableArray<string> entries, int storageVersion)
            {
                Key = key;
                _isEnded = isEnded;
                LeaseEnd = leaseEnd;
                Entries = entries;
                StorageVersion = storageVersion;
            }

            public string Key { get; }

            public bool IsEnded => _isEnded || LeaseEnd <= DateTime.Now;

            public DateTime LeaseEnd { get; }

            public ImmutableArray<string> Entries { get; }

            public int StorageVersion { get; }

            public IStoredSession End()
            {
                if (IsEnded)
                    return this;

                return new Session(Key, isEnded: true, LeaseEnd, Entries, StorageVersion + 1);
            }

            public IStoredSession UpdateLease(DateTime leaseEnd)
            {
                if (_isEnded)
                    return this;

                if (LeaseEnd <= DateTime.Now)
                    return new Session(Key, isEnded: true, LeaseEnd, Entries, StorageVersion + 1);

                if (leaseEnd < LeaseEnd)
                    return this;

                return new Session(Key, isEnded: false, leaseEnd, Entries, StorageVersion + 1);
            }

            public IStoredSession AddEntry(string entry)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                if (IsEnded)
                    throw new InvalidOperationException();

                if (Entries.Contains(entry))
                    return this;

                return new Session(Key, IsEnded, LeaseEnd, Entries.Add(entry), StorageVersion + 1);
            }

            public IStoredSession RemoveEntry(string entry)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                if (!Entries.Contains(entry))
                    return this;

                return new Session(Key, IsEnded, LeaseEnd, Entries.Remove(entry), StorageVersion + 1);
            }
        }

        #endregion
    }
}
