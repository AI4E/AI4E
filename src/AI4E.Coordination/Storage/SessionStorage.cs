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
    public sealed class SessionStorage : ISessionStorage
    {
        private readonly IDatabase _database;

        public SessionStorage(IDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        #region Session

        public async Task<IStoredSession> GetSessionAsync(CoordinationSession session, CancellationToken cancellation)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var compactStringSession = session.ToString();

            var storedSession = await _database.GetOneAsync<StoredSession>(p => p.Id == compactStringSession, cancellation);

            Assert(storedSession == null || (storedSession as IStoredSession).Session == session);

            return storedSession;
        }

        public IAsyncEnumerable<IStoredSession> GetSessionsAsync(CancellationToken cancellation)
        {
            return _database.GetAsync<StoredSession>(cancellation);
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
