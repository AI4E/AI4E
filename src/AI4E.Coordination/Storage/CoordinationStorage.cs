using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Storage;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination.Storage
{
    public sealed class CoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly IDatabase _database;
        private readonly IStoredSessionManager _storedSessionManager;

        public CoordinationStorage(IDatabase database,
                                   IStoredSessionManager storedSessionManager)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (storedSessionManager == null)
                throw new ArgumentNullException(nameof(storedSessionManager));

            _database = database;
            _storedSessionManager = storedSessionManager;
        }

        #region Entry

        public async ValueTask<IStoredEntry> GetEntryAsync(
            string key,
            CancellationToken cancellation)
        {
            return await _database.GetOneAsync<SerializedStoredEntry>(p => p.Id == key, cancellation);
        }

        public async ValueTask<IStoredEntry> UpdateEntryAsync(
            IStoredEntry value,
            IStoredEntry comparand,
            CancellationToken cancellation)
        {
            var convertedValue = ConvertValue(value);
            var convertedComparand = ConvertValue(comparand);

            if (await _database.CompareExchangeAsync(convertedValue, convertedComparand, (left, right) => left.StorageVersion == right.StorageVersion, cancellation))
            {
                return comparand;
            }

            return await GetEntryAsync((comparand ?? value).Key, cancellation);
        }

        private SerializedStoredEntry ConvertValue(IStoredEntry value)
        {
            return value as SerializedStoredEntry ?? (value != null ? new SerializedStoredEntry(value) : null);
        }

        #endregion

        #region Session

        public async Task<IStoredSession> GetSessionAsync(CoordinationSession session, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var compactStringSession = session.ToString();

            var storedSession = await _database.GetOneAsync<StoredSession>(p => p.Id == compactStringSession, cancellation);

            Assert(storedSession == null || (storedSession as IStoredSession).Session == session);

            return _storedSessionManager.Copy(storedSession);
        }

        public IAsyncEnumerable<IStoredSession> GetSessionsAsync(CancellationToken cancellation)
        {
            return _database.GetAsync<StoredSession>(cancellation).Select(p => _storedSessionManager.Copy(p));
        }

        public async Task<IStoredSession> UpdateSessionAsync(IStoredSession value, IStoredSession comparand, CancellationToken cancellation)
        {
            var convertedValue = ConvertValue(value);
            var convertedComparand = ConvertValue(comparand);

            if (await _database.CompareExchangeAsync(convertedValue, convertedComparand, (left, right) => left.StorageVersion == right.StorageVersion, cancellation))
            {
                return comparand;
            }

            return await GetSessionAsync((comparand ?? value).Session, cancellation);
        }

        private StoredSession ConvertValue(IStoredSession value)
        {
            StoredSession convertedValue = null;

            if (value != null)
            {
                convertedValue = value as StoredSession ?? new StoredSession(value);

                Assert(convertedValue != null);
                Assert((convertedValue as IStoredSession).Session == value.Session);
            }

            return convertedValue;
        }

        #endregion

        private sealed class SerializedStoredEntry : IStoredEntry
        {
            public SerializedStoredEntry() { }

            public SerializedStoredEntry(IStoredEntry entry)
            {
                Id = entry.Key;
                Value = entry.Value.ToArray();
                ReadLocks = entry.ReadLocks.Select(p => p.ToString()).ToArray();
                WriteLock = entry.WriteLock?.ToString();
                StorageVersion = entry.StorageVersion;
                IsMarkedAsDeleted = entry.IsMarkedAsDeleted;
            }

            public string Id { get; set; }
            public byte[] Value { get; set; }
            public string[] ReadLocks { get; set; }
            public string WriteLock { get; set; }
            public int StorageVersion { get; set; }
            public bool IsMarkedAsDeleted { get; set; }

            string IStoredEntry.Key => Id;
            CoordinationSession? IStoredEntry.WriteLock => WriteLock == null ? default(CoordinationSession?) : CoordinationSession.FromChars(WriteLock.AsSpan());
            ImmutableArray<CoordinationSession> IStoredEntry.ReadLocks => ReadLocks.Select(p => CoordinationSession.FromChars(p.AsSpan())).ToImmutableArray();
            ReadOnlyMemory<byte> IStoredEntry.Value => Value;
        }

        private sealed class StoredSession : IStoredSession
        {
            public StoredSession() { }

            public StoredSession(IStoredSession session)
            {
                Id = session.Session.ToString();
                IsEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                EntryPaths = session.EntryPaths.Select(p => p.EscapedPath.ConvertToString()).ToArray();
                StorageVersion = session.StorageVersion;
            }

            public string Id { get; set; }
            CoordinationSession IStoredSession.Session => CoordinationSession.FromChars(Id.AsSpan());
            public bool IsEnded { get; set; }
            public DateTime LeaseEnd { get; set; }
            public string[] EntryPaths { get; set; }
            public int StorageVersion { get; set; }

            ImmutableArray<CoordinationEntryPath> IStoredSession.EntryPaths => EntryPaths.Select(p => CoordinationEntryPath.FromEscapedPath(p.AsMemory())).ToImmutableArray();
        }
    }
}
