using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class CoordinationStorage : ICoordinationStorage, ISessionStorage
    {
        private readonly IFilterableDatabase _database;
        private readonly IStoredSessionManager _storedSessionManager;
        private readonly IStoredEntryManager _storedEntryManager;

        public CoordinationStorage(IFilterableDatabase database,
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

        public async Task<IStoredSession> GetSessionAsync(string key, CancellationToken cancellation)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            var storedSession = await _database.GetOneAsync<StoredSession>(p => p.Id == key, cancellation);

            return _storedSessionManager.Copy(storedSession);
        }

        // TODO: Use IAsyncEnumerable
        public async Task<IEnumerable<IStoredSession>> GetSessionsAsync(CancellationToken cancellation)
        {
            return await _database.GetAsync<StoredSession>(cancellation).Select(p => _storedSessionManager.Copy(p)).ToList(cancellation);
        }

        public async Task<IStoredSession> UpdateSessionAsync(IStoredSession value, IStoredSession comparand, CancellationToken cancellation)
        {
            var convertedValue = ConvertValue(value);
            var convertedComparand = ConvertValue(comparand);

            if (await _database.CompareExchangeAsync(convertedValue, convertedComparand, (left, right) => left.StorageVersion == right.StorageVersion, cancellation))
            {
                return comparand;
            }

            return await GetSessionAsync((comparand ?? value).Key, cancellation);
        }

        private StoredSession ConvertValue(IStoredSession value)
        {
            StoredSession convertedValue = null;

            if (value != null)
            {
                convertedValue = value as StoredSession ?? new StoredSession(value);

                Assert(convertedValue != null);
                Assert(convertedValue.Id == value.Key);
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
                ReadLocks = entry.ReadLocks.ToArray();
                WriteLock = entry.WriteLock;
                Children = entry.Children.Select(p => p.EscapedSegment.ConvertToString()).ToArray();
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Version = entry.Version;
                StorageVersion = entry.StorageVersion;
                EphemeralOwner = entry.EphemeralOwner;
            }

            public string Id { get; set; }
            CoordinationEntryPath IStoredEntry.Path => CoordinationEntryPath.FromEscapedPath(Id.AsMemory());
            public byte[] Value { get; set; }
            public string[] ReadLocks { get; set; }
            public string WriteLock { get; set; }
            public int Version { get; set; }
            public int StorageVersion { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime LastWriteTime { get; set; }
            public string[] Children { get; set; }
            public string EphemeralOwner { get; set; }

            ReadOnlyMemory<byte> IStoredEntry.Value => Value;
            ImmutableArray<string> IStoredEntry.ReadLocks => ReadLocks.ToImmutableArray();
            ImmutableList<CoordinationEntryPathSegment> IStoredEntry.Children => Children.Select(p => CoordinationEntryPathSegment.FromEscapedSegment(p.AsMemory())).ToImmutableList();

        }

        private sealed class StoredSession : IStoredSession
        {
            public StoredSession() { }

            public StoredSession(IStoredSession session)
            {
                Id = session.Key;
                IsEnded = session.IsEnded;
                LeaseEnd = session.LeaseEnd;
                EntryPaths = session.EntryPaths.Select(p => p.EscapedPath.ConvertToString()).ToArray();
                StorageVersion = session.StorageVersion;
            }

            public string Id { get; set; }
            string IStoredSession.Key => Id;
            public bool IsEnded { get; set; }
            public DateTime LeaseEnd { get; set; }
            public string[] EntryPaths { get; set; }
            public int StorageVersion { get; set; }

            ImmutableArray<CoordinationEntryPath> IStoredSession.EntryPaths => EntryPaths.Select(p => CoordinationEntryPath.FromEscapedPath(p.AsMemory())).ToImmutableArray();
        }
    }
}
