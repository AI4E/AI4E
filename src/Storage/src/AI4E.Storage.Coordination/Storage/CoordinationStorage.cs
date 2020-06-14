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
using AI4E.Storage.Coordination.Session;
using AI4E.Storage;

namespace AI4E.Storage.Coordination.Storage
{
    /// <summary>
    /// Represents the storage for coordination service entries.
    /// </summary>
    public sealed class CoordinationStorage : ICoordinationStorage
    {
        private readonly IDatabase _database;

        /// <summary>
        /// Creates a new instance of the <see cref="CoordinationStorage"/> type.
        /// </summary>
        /// <param name="database">The underlying database that is used to store entries.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="database"/> is null.</exception>
        public CoordinationStorage(IDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        /// <inheritdoc />
        public async ValueTask<IStoredEntry> GetEntryAsync(
            string key,
            CancellationToken cancellation)
        {
            return await _database.GetOneAsync<SerializedStoredEntry>(p => p.Id == key, cancellation);
        }

        /// <inheritdoc />
        public async ValueTask<IStoredEntry> UpdateEntryAsync(
            IStoredEntry value,
            IStoredEntry comparand,
            CancellationToken cancellation)
        {
            var convertedValue = ConvertValue(value);
            var convertedComparand = ConvertValue(comparand);

            if (convertedValue != null && convertedComparand != null && convertedValue.Id != convertedComparand.Id)
            {
                throw new ArgumentException($"The keys of {nameof(value)} and {nameof(comparand)} must be equal.");
            }

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
            SessionIdentifier? IStoredEntry.WriteLock => WriteLock == null ? default(SessionIdentifier?) : SessionIdentifier.FromChars(WriteLock.AsSpan());
            ImmutableArray<SessionIdentifier> IStoredEntry.ReadLocks => ReadLocks.Select(p => SessionIdentifier.FromChars(p.AsSpan())).ToImmutableArray();
            ReadOnlyMemory<byte> IStoredEntry.Value => Value;
        }
    }
}
