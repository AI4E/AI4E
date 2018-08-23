using System;
using System.Collections.Immutable;
using System.Linq;

namespace AI4E.Coordination
{
    public sealed class StoredEntryManager : IStoredEntryManager
    {
        private static readonly ImmutableArray<string> _noReadLocks = ImmutableArray<string>.Empty;
        private static readonly ImmutableArray<string> _noChilds = ImmutableArray<string>.Empty;

        private readonly IDateTimeProvider _dateTimeProvider;

        public StoredEntryManager(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        public IStoredEntry Copy(IStoredEntry storedEntry)
        {
            if (storedEntry == null)
                return null;

            return storedEntry as StoredEntry ?? new StoredEntry(storedEntry);
        }

        public IStoredEntry Create(string key, string session, bool isEphemeral, ReadOnlySpan<byte> value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var currentTime = _dateTimeProvider.GetCurrentTime();

            return new StoredEntry(key,
                                   value.ToArray(), // We need to copy here in order to guarantee immutability.
                                   _noReadLocks,
                                   writeLock: session,
                                   _noChilds,
                                   version: 1,
                                   storageVersion: 1,
                                   ephemeralOwner: isEphemeral ? session : null,
                                   currentTime,
                                   currentTime);
        }

        public IStoredEntry AcquireWriteLock(IStoredEntry storedEntry, string session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.WriteLock == session)
                return storedEntry;

            if (storedEntry.WriteLock != null)
                throw new InvalidOperationException();

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks,
                                   writeLock: session,
                                   storedEntry.Childs,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry ReleaseWriteLock(IStoredEntry storedEntry)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (storedEntry.WriteLock == null)
                return null;

            //if (storedEntry.ReadLocks.Length > 0)
            //    throw new InvalidOperationException();

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   _noReadLocks,
                                   writeLock: null,
                                   storedEntry.Childs,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry AcquireReadLock(IStoredEntry storedEntry, string session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.WriteLock != null)
                throw new InvalidOperationException();

            if (storedEntry.ReadLocks.Contains(session))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks.Add(session),
                                   writeLock: null,
                                   storedEntry.Childs,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry ReleaseReadLock(IStoredEntry storedEntry, string session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.ReadLocks.Length == 0 || !storedEntry.ReadLocks.Contains(session))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks.Remove(session),
                                   storedEntry.WriteLock,
                                   storedEntry.Childs,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry Remove(IStoredEntry storedEntry)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (storedEntry.ReadLocks.Length > 0)
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            if (storedEntry.Childs.Any())
                throw new InvalidOperationException();

            return null;
        }

        public IStoredEntry SetValue(IStoredEntry storedEntry, ReadOnlySpan<byte> value)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (storedEntry.ReadLocks.Length > 0)
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            var writeTime = _dateTimeProvider.GetCurrentTime();

            if (writeTime < storedEntry.CreationTime)
            {
                writeTime = storedEntry.CreationTime;
            }

            return new StoredEntry(storedEntry.Path,
                                   value.ToArray(), // We need to copy here in order to guarantee immutability.
                                   _noReadLocks,
                                   storedEntry.WriteLock,
                                   storedEntry.Childs,
                                   storedEntry.Version + 1,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   writeTime);
        }

        public IStoredEntry AddChild(IStoredEntry storedEntry, string name)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (storedEntry.ReadLocks.Length > 0)
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            if (storedEntry.Childs.Contains(name))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks,
                                   storedEntry.WriteLock,
                                   storedEntry.Childs.Add(name),
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry RemoveChild(IStoredEntry storedEntry, string name)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (storedEntry.ReadLocks.Length > 0)
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            if (!storedEntry.Childs.Contains(name))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks,
                                   storedEntry.WriteLock,
                                   storedEntry.Childs.Remove(name),
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        private sealed class StoredEntry : IStoredEntry
        {
            public StoredEntry(IStoredEntry entry)
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

            public StoredEntry(string key,
                          ReadOnlyMemory<byte> value,
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

            public string Path { get; }

            public ReadOnlyMemory<byte> Value { get; }

            public ImmutableArray<string> ReadLocks { get; }

            public string WriteLock { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public ImmutableArray<string> Childs { get; }

            public int Version { get; }

            public string EphemeralOwner { get; }

            public int StorageVersion { get; }
        }
    }
}
