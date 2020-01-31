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
    internal sealed class MetadataCache<TId, TEntry>
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

            _cache = new Dictionary<TId, CacheEntry>();
        }

        public IEnumerable<MetadataCacheEntry<TEntry>> GetTrackedEntries()
        {
            var resultBuilder = ImmutableList.CreateBuilder<MetadataCacheEntry<TEntry>>();

            foreach (var cacheEntry in _cache.Values)
            {
                TEntry originalEntry = null,
                       entry = null;
                MetadataCacheEntryState entryState;

                if (!cacheEntry.Touched)
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);
                    originalEntry = cacheEntry.OriginalEntry.DeepClone();
                    entry = originalEntry;
                    entryState = MetadataCacheEntryState.Unchanged;
                }
                else if (cacheEntry.Entry is null)
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);
                    originalEntry = cacheEntry.OriginalEntry.DeepClone();
                    entryState = MetadataCacheEntryState.Deleted;
                }
                else if (cacheEntry.OriginalEntry is null)
                {
                    Debug.Assert(cacheEntry.Entry != null);
                    entry = cacheEntry.Entry.DeepClone();
                    entryState = MetadataCacheEntryState.Created;
                }
                else
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);
                    Debug.Assert(cacheEntry.Entry != null);
                    originalEntry = cacheEntry.OriginalEntry.DeepClone();
                    entry = cacheEntry.Entry.DeepClone();
                    entryState = MetadataCacheEntryState.Updated;
                }

                resultBuilder.Add(new MetadataCacheEntry<TEntry>(entry, originalEntry, entryState));
            }

            return resultBuilder.ToImmutable();
        }

        public MetadataCacheEntryState GetEntryState(TEntry entry)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                return MetadataCacheEntryState.Untracked;
            }

            if (!cacheEntry.Touched)
            {
                return MetadataCacheEntryState.Unchanged;
            }
            if (cacheEntry.Entry is null)
            {
                return MetadataCacheEntryState.Deleted;
            }
            if (cacheEntry.OriginalEntry is null)
            {
                return MetadataCacheEntryState.Created;
            }

            return MetadataCacheEntryState.Updated;
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
            var cacheEntry = await GetCacheEntryAsync(id, cancellation);
            return cacheEntry.Entry;
        }

        private async ValueTask<CacheEntry> GetCacheEntryAsync(
           TId id,
           CancellationToken cancellation)
        {
            var entry = await QueryEntryAsync(id, cancellation);

            if (entry != null)
            {
                var originalEntry = entry.DeepClone();
                var cacheEntry = new CacheEntry(originalEntry, entry);
                _cache[id] = cacheEntry;

                return cacheEntry;
            }

            return default;
        }

        private async ValueTask<TEntry> QueryEntryAsync(
            TId id, CancellationToken cancellation)
        {
            return await _database.GetOneAsync(_queryBuilder(id), cancellation);
        }

        public async ValueTask UpdateEntryAsync(TEntry entry, CancellationToken cancellation)
        {
            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                cacheEntry = await GetCacheEntryAsync(id, cancellation);
            }

            _cache[id] = cacheEntry.Update(entry);
        }

        public async ValueTask DeleteEntryAsync(TEntry entry, CancellationToken cancellation)
        {
            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                cacheEntry = await GetCacheEntryAsync(id, cancellation);
            }

            if (cacheEntry.OriginalEntry is null)
            {
                _cache.Remove(id);
            }
            else
            {
                _cache[id] = cacheEntry.Delete();
            }
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(TEntry originalEntry, TEntry entry) : this(originalEntry, entry, touched: false)
            { }

            private CacheEntry(TEntry originalEntry, TEntry entry, bool touched)
            {
                Debug.Assert(originalEntry != null || entry != null);

                OriginalEntry = originalEntry;
                Entry = entry;
                Touched = touched;
            }

            public TEntry OriginalEntry { get; }
            public TEntry Entry { get; }
            public bool Touched { get; }

            public CacheEntry Update(TEntry entry)
            {
                return new CacheEntry(OriginalEntry, entry, touched: true);
            }

            public CacheEntry Delete()
            {
                return new CacheEntry(OriginalEntry, null, touched: true);
            }
        }
    }

    internal readonly struct MetadataCacheEntry<TEntry>
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

    internal enum MetadataCacheEntryState
    {
        Untracked = default,
        Unchanged,
        Created,
        Deleted,
        Updated
    }
}
