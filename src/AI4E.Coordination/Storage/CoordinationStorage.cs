/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

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
                ReadLocks = GetReadLocks(entry);
                WriteLock = entry.WriteLock?.ToString();
                StorageVersion = entry.StorageVersion;
                IsMarkedAsDeleted = entry.IsMarkedAsDeleted;
            }

            private static string[] GetReadLocks(IStoredEntry entry)
            {
                if (entry.ReadLocks.IsDefaultOrEmpty)
                    return Array.Empty<string>();

                return entry.ReadLocks.Select(p => p.ToString()).ToArray();
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
