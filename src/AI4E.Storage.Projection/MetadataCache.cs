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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

namespace AI4E.Storage.Projection
{
    public sealed class MetadataCache<TId, TEntry>
        where TEntry : class
    {
        private readonly IDatabase _database;
        private readonly Func<TEntry, TId> _idAccessor;
        private readonly Func<TId, Expression<Func<TEntry, bool>>> _queryBuilder;
        private readonly IDictionary<TId, CacheEntry> _cache;

        public MetadataCache(
            IDatabase database,
            Func<TEntry, TId> idAccessor,
            Func<TId, Expression<Func<TEntry, bool>>> queryBuilder)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            if (idAccessor is null)
                throw new ArgumentNullException(nameof(idAccessor));

            if (queryBuilder is null)
                throw new ArgumentNullException(nameof(queryBuilder));

            _database = database;
            _idAccessor = idAccessor;
            _queryBuilder = queryBuilder;
        }

        public IEnumerable<MetadataCacheEntry<TEntry>> GetEntries()
        {
            var resultBuilder = ImmutableList.CreateBuilder<MetadataCacheEntry<TEntry>>();

            foreach (var cacheEntry in _cache.Values)
            {
                if (!cacheEntry.Touched)
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);

                    var originalEntryCopy = cacheEntry.OriginalEntry.DeepClone();
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(originalEntryCopy, originalEntryCopy, MetadataCacheEntryState.Unchanged));
                }
                else if (cacheEntry.Entry is null)
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);

                    var originalEntryCopy = cacheEntry.OriginalEntry.DeepClone();
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(originalEntryCopy, originalEntryCopy, MetadataCacheEntryState.Deleted));
                }
                else if (cacheEntry.OriginalEntry is null)
                {
                    Debug.Assert(cacheEntry.Entry != null);
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(cacheEntry.Entry.DeepClone(), originalEntry: null, MetadataCacheEntryState.Created));
                }
                else
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);
                    Debug.Assert(cacheEntry.Entry != null);
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(cacheEntry.Entry.DeepClone(), cacheEntry.OriginalEntry.DeepClone(), MetadataCacheEntryState.Updated));
                }
            }

            return resultBuilder.ToImmutable();
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public ValueTask<TEntry> GetEntryAsync(
            TId id,
            CancellationToken cancellation)
        {
            if (_cache.TryGetValue(id, out var cacheEntry))
            {
                return new ValueTask<TEntry>(cacheEntry.Entry.DeepClone());
            }

            return GetEntryCoreAsync(id, cancellation);
        }

        private async ValueTask<TEntry> GetEntryCoreAsync(
            TId id,
            CancellationToken cancellation)
        {
            var entry = await QueryEntryAsync(id, cancellation);
            var touched = false;

            if (entry != null)
            {
                var originalEntry = entry.DeepClone();
                var cacheEntry = new CacheEntry(originalEntry, entry, touched);
                _cache[id] = cacheEntry;
            }

            return entry;
        }

        private async ValueTask<TEntry> QueryEntryAsync(
            TId id, CancellationToken cancellation)
        {
            return await _database.GetOneAsync(_queryBuilder(id), cancellation);
        }

        public void UpdateEntry(TEntry entry)
        {
            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                cacheEntry = default;
            }

            _cache[id] = new CacheEntry(cacheEntry.OriginalEntry, entry.DeepClone(), touched: true);
        }

        public void DeleteEntry(TEntry entry)
        {
            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                return;
            }

            if (cacheEntry.OriginalEntry is null)
            {
                _cache.Remove(id);
            }

            _cache[id] = new CacheEntry(cacheEntry.OriginalEntry, null, touched: true);
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(TEntry originalEntry, TEntry entry, bool touched)
            {
                Debug.Assert(originalEntry != null || entry != null);

                OriginalEntry = originalEntry;
                Entry = entry;
                Touched = touched;
            }

            public TEntry OriginalEntry { get; }
            public TEntry Entry { get; }
            public bool Touched { get; }
        }
    }

    public readonly struct MetadataCacheEntry<TEntry>
    {
        public MetadataCacheEntry(TEntry entry, TEntry originalEntry, MetadataCacheEntryState state)
        {
            Entry = entry;
            OriginalEntry = originalEntry;
            State = state;
        }

        public TEntry Entry { get; }
        public TEntry OriginalEntry { get; }
        public MetadataCacheEntryState State { get; }
    }

    public enum MetadataCacheEntryState
    {
        Unchanged,
        Created,
        Deleted,
        Updated
    }
}
