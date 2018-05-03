using System;
using System.Collections.Immutable;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class StoredSessionManager : IStoredSessionManager
    {
        private static readonly ImmutableArray<string> _noEntries = ImmutableArray<string>.Empty;

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

            return new StoredSession(storedSession.Key, isEnded: true, storedSession.LeaseEnd, storedSession.Entries, storedSession.StorageVersion + 1);
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

            return new StoredSession(storedSession.Key, isEnded: false, leaseEnd, storedSession.Entries, storedSession.StorageVersion + 1);
        }

        public IStoredSession AddEntry(IStoredSession storedSession, string entry)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (IsEnded(storedSession))
                throw new InvalidOperationException();

            if (storedSession.Entries.Contains(entry))
                return storedSession;

            return new StoredSession(storedSession.Key, storedSession.IsEnded, storedSession.LeaseEnd, storedSession.Entries.Add(entry), storedSession.StorageVersion + 1);
        }

        public IStoredSession RemoveEntry(IStoredSession storedSession, string entry)
        {
            if (storedSession == null)
                throw new ArgumentNullException(nameof(storedSession));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!storedSession.Entries.Contains(entry))
                return storedSession;

            return new StoredSession(storedSession.Key, storedSession.IsEnded, storedSession.LeaseEnd, storedSession.Entries.Remove(entry), storedSession.StorageVersion + 1);
        }

        private sealed class StoredSession : IStoredSession
        {
            public StoredSession(IStoredSession session)
            {
                Key = session.Key;
                IsEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                Entries = session.Entries;
                StorageVersion = session.StorageVersion;
            }

            public StoredSession(string key, bool isEnded, DateTime leaseEnd, ImmutableArray<string> entries, int storageVersion)
            {
                Assert(key != null);

                Key = key;
                IsEnded = isEnded;
                LeaseEnd = leaseEnd;
                Entries = entries;
                StorageVersion = storageVersion;
            }

            public string Key { get; }

            public bool IsEnded { get; }

            public DateTime LeaseEnd { get; }

            public ImmutableArray<string> Entries { get; }

            public int StorageVersion { get; }
        }
    }
}
