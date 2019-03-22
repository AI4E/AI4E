using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Storage;

namespace AI4E.Coordination.Storage
{
    public sealed class CoordinationStorage : ICoordinationStorage
    {
        private readonly IDatabase _database;

        public CoordinationStorage(IDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

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
    }
}
