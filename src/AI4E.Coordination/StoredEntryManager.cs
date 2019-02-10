using System;
using System.Collections.Immutable;
using System.Linq;

namespace AI4E.Coordination
{
    public sealed class StoredEntryManager : IStoredEntryManager
    {
        private static readonly ImmutableArray<Session> _noReadLocks = ImmutableArray<Session>.Empty;
        private static readonly ImmutableList<CoordinationEntryPathSegment> _noChilds = ImmutableList<CoordinationEntryPathSegment>.Empty;

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

        public IStoredEntry Create(CoordinationEntryPath path, Session session, bool isEphemeral, ReadOnlySpan<byte> value)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var currentTime = _dateTimeProvider.GetCurrentTime();

            return new StoredEntry(path,
                                   value.ToArray(), // We need to copy here in order to guarantee immutability.
                                   _noReadLocks,
                                   writeLock: session,
                                   _noChilds,
                                   version: 1,
                                   storageVersion: 1,
                                   ephemeralOwner: isEphemeral ? session : default(Session?),
                                   currentTime,
                                   currentTime);
        }

        public IStoredEntry AcquireWriteLock(IStoredEntry storedEntry, Session session)
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
                                   storedEntry.Children,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry ReleaseWriteLock(IStoredEntry storedEntry, Session session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (storedEntry.WriteLock == null)
                return null;

            if (storedEntry.WriteLock != session)
                throw new InvalidOperationException();

            // We cannot check this here, as this is called when the session that holds write-lock is terminates too.
            // In this case, there may be read-locks present.
            //if (storedEntry.ReadLocks.Length != 0 && (storedEntry.ReadLocks.Length > 1 || storedEntry.ReadLocks.First() != storedEntry.WriteLock))       
            //    throw new InvalidOperationException();

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   _noReadLocks,
                                   writeLock: null,
                                   storedEntry.Children,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry AcquireReadLock(IStoredEntry storedEntry, Session session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.WriteLock != null && storedEntry.WriteLock != session)
                throw new InvalidOperationException();

            if (storedEntry.ReadLocks.Contains(session))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks.Add(session),
                                   writeLock: null,
                                   storedEntry.Children,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry ReleaseReadLock(IStoredEntry storedEntry, Session session)
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
                                   storedEntry.Children,
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry Remove(IStoredEntry storedEntry, Session session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (storedEntry.ReadLocks.Length != 0 && (storedEntry.ReadLocks.Length > 1 || storedEntry.ReadLocks.First() != session))
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            if (storedEntry.Children.Any())
                throw new InvalidOperationException();

            return null;
        }

        public IStoredEntry SetValue(IStoredEntry storedEntry, ReadOnlySpan<byte> value, Session session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.ReadLocks.Length != 0 && (storedEntry.ReadLocks.Length > 1 || storedEntry.ReadLocks.First() != session))
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
                                   storedEntry.Children,
                                   storedEntry.Version + 1,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   writeTime);
        }

        public IStoredEntry AddChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child, Session session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (child == default)
                throw new ArgumentDefaultException(nameof(child));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.ReadLocks.Length != 0 && (storedEntry.ReadLocks.Length > 1 || storedEntry.ReadLocks.First() != session))
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            if (storedEntry.Children.Contains(child))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks,
                                   storedEntry.WriteLock,
                                   storedEntry.Children.Add(child),
                                   storedEntry.Version,
                                   storedEntry.StorageVersion + 1,
                                   storedEntry.EphemeralOwner,
                                   storedEntry.CreationTime,
                                   storedEntry.LastWriteTime);
        }

        public IStoredEntry RemoveChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child, Session session)
        {
            if (storedEntry == null)
                throw new ArgumentNullException(nameof(storedEntry));

            if (child == default)
                throw new ArgumentDefaultException(nameof(child));

            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (storedEntry.ReadLocks.Length != 0 && (storedEntry.ReadLocks.Length > 1 || storedEntry.ReadLocks.First() != session))
                throw new InvalidOperationException();

            if (storedEntry.WriteLock == null)
                throw new InvalidOperationException();

            if (!storedEntry.Children.Contains(child))
                return storedEntry;

            return new StoredEntry(storedEntry.Path,
                                   storedEntry.Value,
                                   storedEntry.ReadLocks,
                                   storedEntry.WriteLock,
                                   storedEntry.Children.Remove(child),
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
                Children = entry.Children;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Version = entry.Version;
                StorageVersion = entry.StorageVersion;
                EphemeralOwner = entry.EphemeralOwner;
            }

            public StoredEntry(CoordinationEntryPath path,
                               ReadOnlyMemory<byte> value,
                               ImmutableArray<Session> readLocks,
                               Session? writeLock,
                               ImmutableList<CoordinationEntryPathSegment> children,
                               int version,
                               int storageVersion,
                               Session? ephemeralOwner,
                               DateTime creationTime,
                               DateTime lastWriteTime)
            {
                Path = path;
                Value = value;
                ReadLocks = readLocks;
                WriteLock = writeLock;
                Children = children;
                Version = version;
                StorageVersion = storageVersion;
                EphemeralOwner = ephemeralOwner;
                CreationTime = creationTime;
                LastWriteTime = lastWriteTime;
            }

            public CoordinationEntryPath Path { get; }

            public ReadOnlyMemory<byte> Value { get; }

            public ImmutableArray<Session> ReadLocks { get; }

            public Session? WriteLock { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public ImmutableList<CoordinationEntryPathSegment> Children { get; }

            public int Version { get; }

            public Session? EphemeralOwner { get; }

            public int StorageVersion { get; }
        }
    }
}
