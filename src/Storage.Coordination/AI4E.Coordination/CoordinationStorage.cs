using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class CoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly IDatabase _database;
        private readonly IStoredSessionManager _storedSessionManager;
        private readonly IStoredEntryManager _storedEntryManager;

        public CoordinationStorage(IDatabase database,
                                   IStoredSessionManager storedSessionManager,
                                   IStoredEntryManager storedEntryManager)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (storedSessionManager == null)
                throw new ArgumentNullException(nameof(storedSessionManager));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            _database = database;
            _storedSessionManager = storedSessionManager;
            _storedEntryManager = storedEntryManager;
        }

        #region Entry

        public async Task<IStoredEntry> GetEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var escapedPath = path.EscapedPath.ConvertToString();
            var storedEntry = await _database.GetOneAsync<StoredEntry>(p => p.Id == escapedPath, cancellation);

            return _storedEntryManager.Copy(storedEntry);
        }

        public async Task<IStoredEntry> UpdateEntryAsync(IStoredEntry value, IStoredEntry comparand, CancellationToken cancellation)
        {
            var convertedValue = ConvertValue(value);
            var convertedComparand = ConvertValue(comparand);

            if (await _database.CompareExchangeAsync(convertedValue, convertedComparand, (left, right) => left.StorageVersion == right.StorageVersion, cancellation))
            {
                return comparand;
            }

            return await GetEntryAsync((comparand ?? value).Path, cancellation);
        }

        private StoredEntry ConvertValue(IStoredEntry value)
        {
            StoredEntry convertedValue = null;

            if (value != null)
            {
                convertedValue = value as StoredEntry ?? new StoredEntry(value);

                Assert(convertedValue != null);
                Assert(convertedValue.Id == value.Path.EscapedPath.ConvertToString());
            }

            return convertedValue;
        }

        #endregion

        #region Session

        public async Task<IStoredSession> GetSessionAsync(Session session, CancellationToken cancellation)
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

        private sealed class StoredEntry : IStoredEntry
        {
            public StoredEntry() { }

            public StoredEntry(IStoredEntry entry)
            {
                Id = entry.Path.EscapedPath.ConvertToString();
                Value = entry.Value.ToArray();
                ReadLocks = entry.ReadLocks.Select(p => p.ToString()).ToArray();
                WriteLock = entry.WriteLock?.ToString();
                Children = entry.Children.Select(p => p.EscapedSegment.ConvertToString()).ToArray();
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Version = entry.Version;
                StorageVersion = entry.StorageVersion;
                EphemeralOwner = entry.EphemeralOwner?.ToString();
            }

            public string Id { get; set; }
            CoordinationEntryPath IStoredEntry.Path => CoordinationEntryPath.FromEscapedPath(Id.AsMemory());
            public byte[] Value { get; set; }
            public string[] ReadLocks { get; set; }
            public string WriteLock { get; set; }
            Session? IStoredEntry.WriteLock => WriteLock == null ? default(Session?) : Session.FromChars(WriteLock.AsSpan());

            public int Version { get; set; }
            public int StorageVersion { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime LastWriteTime { get; set; }
            public string[] Children { get; set; }
            public string EphemeralOwner { get; set; }
            Session? IStoredEntry.EphemeralOwner => EphemeralOwner == null ? default(Session?) : Session.FromChars(EphemeralOwner.AsSpan());

            ReadOnlyMemory<byte> IStoredEntry.Value => Value;
            ImmutableArray<Session> IStoredEntry.ReadLocks => ReadLocks.Select(p => Session.FromChars(p.AsSpan())).ToImmutableArray();
            ImmutableList<CoordinationEntryPathSegment> IStoredEntry.Children => Children.Select(p => CoordinationEntryPathSegment.FromEscapedSegment(p.AsMemory())).ToImmutableList();

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
            Session IStoredSession.Session => Session.FromChars(Id.AsSpan());
            public bool IsEnded { get; set; }
            public DateTime LeaseEnd { get; set; }
            public string[] EntryPaths { get; set; }
            public int StorageVersion { get; set; }

            ImmutableArray<CoordinationEntryPath> IStoredSession.EntryPaths => EntryPaths.Select(p => CoordinationEntryPath.FromEscapedPath(p.AsMemory())).ToImmutableArray();
        }
    }
}
