﻿using System;
using System.Collections.Immutable;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class StoredSessionManager : IStoredSessionManager
    {
        private static readonly ImmutableArray<CoordinationEntryPath> _noEntries = ImmutableArray<CoordinationEntryPath>.Empty;

        private readonly IDateTimeProvider _dateTimeProvider;

        public StoredSessionManager(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        public IStoredSession Copy(IStoredSession storedSession)
        {
            if (storedSession == null)
                return null;

            return storedSession as StoredSession ?? new StoredSession(storedSession);
        }

        public IStoredSession Begin(string key, DateTime leaseEnd)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return new StoredSession(key,
                                     isEnded: false,
                                     leaseEnd,
                                     _noEntries,
                                     storageVersion: 1);
        }

        public bool IsEnded(IStoredSession storedSession)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            return storedSession.IsEnded || storedSession.LeaseEnd <= _dateTimeProvider.GetCurrentTime();
        }

        public IStoredSession End(IStoredSession storedSession)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            if (IsEnded(storedSession))
                return storedSession;

            return new StoredSession(storedSession.Key, isEnded: true, storedSession.LeaseEnd, storedSession.EntryPaths, storedSession.StorageVersion + 1);
        }

        public IStoredSession UpdateLease(IStoredSession storedSession, DateTime leaseEnd)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            if (IsEnded(storedSession))
                return storedSession;

            //if (LeaseEnd <= _dateTimeProvider.GetCurrentTime())
            //    return new StoredSession(_dateTimeProvider, Key, isEnded: true, LeaseEnd, Entries, StorageVersion + 1);

            if (leaseEnd <= storedSession.LeaseEnd)
                return storedSession;

            return new StoredSession(storedSession.Key, isEnded: false, leaseEnd, storedSession.EntryPaths, storedSession.StorageVersion + 1);
        }

        public IStoredSession AddEntry(IStoredSession storedSession, CoordinationEntryPath entryPath)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            if (IsEnded(storedSession))
                throw new InvalidOperationException();

            if (storedSession.EntryPaths.Contains(entryPath))
                return storedSession;

            return new StoredSession(storedSession.Key, storedSession.IsEnded, storedSession.LeaseEnd, storedSession.EntryPaths.Add(entryPath), storedSession.StorageVersion + 1);
        }

        public IStoredSession RemoveEntry(IStoredSession storedSession, CoordinationEntryPath entryPath)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            if (!storedSession.EntryPaths.Contains(entryPath))
                return storedSession;

            return new StoredSession(storedSession.Key, storedSession.IsEnded, storedSession.LeaseEnd, storedSession.EntryPaths.Remove(entryPath), storedSession.StorageVersion + 1);
        }

        private sealed class StoredSession : IStoredSession
        {
            public StoredSession(IStoredSession session)
            {
                Key = session.Key;
                IsEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                EntryPaths = session.EntryPaths;
                StorageVersion = session.StorageVersion;
            }

            public StoredSession(string key, bool isEnded, DateTime leaseEnd, ImmutableArray<CoordinationEntryPath> entryPaths, int storageVersion)
            {
                Assert(key != null);

                Key = key;
                IsEnded = isEnded;
                LeaseEnd = leaseEnd;
                EntryPaths = entryPaths;
                StorageVersion = storageVersion;
            }

            public string Key { get; }

            public bool IsEnded { get; }

            public DateTime LeaseEnd { get; }

            public ImmutableArray<CoordinationEntryPath> EntryPaths { get; }

            public int StorageVersion { get; }
        }
    }
}
