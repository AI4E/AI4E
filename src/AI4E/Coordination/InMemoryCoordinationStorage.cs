using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class InMemoryCoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly ConcurrentDictionary<string, Entry> _entries = new ConcurrentDictionary<string, Entry>();
        private readonly ConcurrentDictionary<string, Session> _sessions = new ConcurrentDictionary<string, Session>();

        public InMemoryCoordinationStorage() { }

        #region Entry

        public ICoordinationEntry CreateEntry(string path, string session, bool isEphemeral, byte[] value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new Entry(path, session, isEphemeral, value.ToImmutableArray());
        }

        public Task<ICoordinationEntry> UpdateEntryAsync(ICoordinationEntry comparand, ICoordinationEntry value, CancellationToken cancellation)
        {
            Entry desired = null;

            if (value != null)
            {
                if (comparand != null && value.Path != comparand.Path)
                {
                    throw new ArgumentException("The key of the comparand must be equal to the key of the new value.");
                }

                desired = value as Entry ?? new Entry(value);

                Assert(desired != null);
                Assert(desired.Path == value.Path);
            }

            if (comparand == null)
            {
                if (desired == null)
                {
                    throw new ArgumentException("Either comparand or value may be null but not both.");
                }

                if (!_entries.TryAdd(value.Path, desired) && TryGetEntry(comparand.Path, out var e))
                {
                    return Task.FromResult(e);
                }
            }
            else
            {
                if (comparand is Entry existing && _entries.TryUpdate(comparand.Path, desired, existing))
                {
                    return Task.FromResult(comparand);
                }

                if (TryGetEntry(comparand.Path, out var entry))
                {
                    return Task.FromResult(entry);
                }
            }

            return Task.FromResult<ICoordinationEntry>(null);
        }

        public Task<ICoordinationEntry> GetEntryAsync(string path, CancellationToken cancellation)
        {
            if (_entries.TryGetValue(path, out var entry))
            {
                Assert(entry != null);
                Assert(entry.Path == path);

                return Task.FromResult<ICoordinationEntry>(entry);
            }

            return Task.FromResult<ICoordinationEntry>(null);
        }

        private bool TryGetEntry(string path, out ICoordinationEntry entry)
        {
            if (_entries.TryGetValue(path, out var e))
            {
                Assert(e != null);
                Assert(e.Path == path);

                entry = e;
                return true;
            }

            entry = default;
            return false;
        }

        private sealed class Entry : ICoordinationEntry
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

                if (isEphemeral)
                {
                    EphemeralOwner = session;
                }
            }

            public Entry(ICoordinationEntry entry)
            {
                Path = entry.Path;
                Value = entry.Value;
                ReadLocks = entry.ReadLocks;
                WriteLock = entry.WriteLock;
                Childs = entry.Childs;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Version = entry.Version;
                EphemeralOwner = entry.EphemeralOwner;
            }

            private Entry(string key,
                          ImmutableArray<byte> value,
                          ImmutableArray<string> readLocks,
                          string writeLock,
                          ImmutableArray<string> childs,
                          int version,
                          string ephemeralOwner,
                          DateTime creationTime)
            {
                Path = key;
                Value = value;
                ReadLocks = readLocks;
                WriteLock = writeLock;
                Childs = childs;
                Version = version;
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

            #endregion

            public ICoordinationEntry AcquireWriteLock(string session)
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                if (WriteLock == session)
                    return this;

                if (WriteLock != null)
                    throw new InvalidOperationException();

                return new Entry(Path, Value, ReadLocks, writeLock: session, Childs, Version, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public ICoordinationEntry ReleaseWriteLock()
            {
                if (WriteLock == null)
                    return null;

                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                return new Entry(Path, Value, _noReadLocks, writeLock: null, Childs, Version, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public ICoordinationEntry AcquireReadLock(string session)
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                if (WriteLock != null)
                    throw new InvalidOperationException();

                if (ReadLocks.Contains(session))
                    return this;

                return new Entry(Path, Value, ReadLocks.Add(session), writeLock: null, Childs, Version, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public ICoordinationEntry ReleaseReadLock(string session)
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                if (ReadLocks.Length == 0 || !ReadLocks.Contains(session))
                    return this;

                return new Entry(Path, Value, ReadLocks.Remove(session), WriteLock, Childs, Version, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public ICoordinationEntry Remove()
            {
                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                if (Childs.Any())
                    throw new InvalidOperationException();

                return null;
            }

            public ICoordinationEntry SetValue(ImmutableArray<byte> value)
            {
                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                return new Entry(Path, value, _noReadLocks, WriteLock, Childs, Version + 1, EphemeralOwner, CreationTime);
            }

            public ICoordinationEntry AddChild(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));

                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                if (Childs.Contains(name))
                    return this;

                return new Entry(Path, Value, ReadLocks, WriteLock, Childs.Add(name), Version, EphemeralOwner, CreationTime, LastWriteTime);
            }

            public ICoordinationEntry RemoveChild(string name)
            {
                if (name == null)
                    throw new ArgumentNullException(nameof(name));

                if (ReadLocks.Length > 0)
                    throw new InvalidOperationException();

                if (WriteLock == null)
                    throw new InvalidOperationException();

                if (!Childs.Contains(name))
                    return this;

                return new Entry(Path, Value, ReadLocks, WriteLock, Childs.Remove(name), Version, EphemeralOwner, CreationTime, LastWriteTime);
            }
        }

        #endregion

        #region Session

        public ISession CreateSession(string key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return new Session(key);
        }

        public Task<ISession> UpdateSessionAsync(ISession comparand, ISession value, CancellationToken cancellation)
        {
            Session desired = null;

            if (value != null)
            {
                if (comparand != null && value.Key != comparand.Key)
                {
                    throw new ArgumentException("The key of the comparand must be equal to the key of the new value.");
                }

                desired = value as Session ?? new Session(value);

                Assert(desired != null);
                Assert(desired.Key == value.Key);
            }

            if (comparand == null)
            {
                if (desired == null)
                {
                    throw new ArgumentException("Either comparand or value may be null but not both.");
                }

                if (!_sessions.TryAdd(value.Key, desired) && TryGetSession(comparand.Key, out var e))
                {
                    return Task.FromResult(e);
                }
            }
            else
            {
                if (comparand is Session existing && _sessions.TryUpdate(comparand.Key, desired, existing))
                {
                    return Task.FromResult(comparand);
                }

                if (TryGetSession(comparand.Key, out var entry))
                {
                    return Task.FromResult(entry);
                }
            }

            return Task.FromResult<ISession>(null);
        }

        public Task<ISession> GetSessionAsync(string key, CancellationToken cancellation)
        {
            if (_sessions.TryGetValue(key, out var session))
            {
                Assert(session != null);
                Assert(session.Key == key);

                return Task.FromResult<ISession>(session);
            }

            return Task.FromResult<ISession>(null);
        }

        private bool TryGetSession(string key, out ISession session)
        {
            if (_sessions.TryGetValue(key, out var e))
            {
                Assert(e != null);
                Assert(e.Key == key);

                session = e;
                return true;
            }

            session = default;
            return false;
        }

        private sealed class Session : ISession
        {
            private readonly bool _isEnded;

            public Session(string key)
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                Key = key;
                LeaseEnd = DateTime.Now + TimeSpan.FromSeconds(30);
                _isEnded = false;
                Entries = ImmutableArray<string>.Empty;
            }

            public Session(ISession session)
            {
                Key = session.Key;
                _isEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                Entries = session.Entries;
            }

            private Session(string key, bool isEnded, DateTime leaseEnd, ImmutableArray<string> entries)
            {
                Key = key;
                _isEnded = isEnded;
                LeaseEnd = leaseEnd;
                Entries = entries;
            }

            public string Key { get; }

            public bool IsEnded => _isEnded || LeaseEnd <= DateTime.Now;

            public DateTime LeaseEnd { get; }

            public ImmutableArray<string> Entries { get; }

            public ISession End()
            {
                if (IsEnded)
                    return this;

                return new Session(Key, isEnded: true, LeaseEnd, Entries);
            }

            public ISession UpdateLease(DateTime leaseEnd)
            {
                if (_isEnded)
                    return this;

                if (LeaseEnd <= DateTime.Now)
                    return new Session(Key, isEnded: true, LeaseEnd, Entries);

                if (leaseEnd < LeaseEnd)
                    return this;

                return new Session(Key, isEnded: false, leaseEnd, Entries);
            }

            public ISession AddEntry(string entry)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                if (IsEnded)
                    throw new InvalidOperationException();

                if (Entries.Contains(entry))
                    return this;

                return new Session(Key, IsEnded, LeaseEnd, Entries.Add(entry));
            }

            public ISession RemoveEntry(string entry)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                if (!Entries.Contains(entry))
                    return this;

                return new Session(Key, IsEnded, LeaseEnd, Entries.Remove(entry));
            }
        }

        #endregion
    }
}
