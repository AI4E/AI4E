/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Concurrent;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Coordination
{
    public sealed class CoordinationEntryCache
    {
        private readonly ConcurrentDictionary<CoordinationEntryPath, CacheEntry> _cache;

        public CoordinationEntryCache()
        {
            _cache = new ConcurrentDictionary<CoordinationEntryPath, CacheEntry>();
        }

        public bool TryGetEntry(CoordinationEntryPath path, out ICacheEntry cacheEntry)
        {
            if (_cache.TryGetValue(path, out var result))
            {
                cacheEntry = result;
                return true;
            }

            cacheEntry = null;
            return false;
        }

        public ICacheEntry GetEntry(CoordinationEntryPath path)
        {
            return _cache.GetOrAdd(path, _ => new CacheEntry(path));
        }

        public ICacheEntry InvalidateEntry(ICacheEntry cacheEntry)
        {
            if (cacheEntry == null)
                throw new ArgumentNullException(nameof(cacheEntry));

            return InvalidateEntry(cacheEntry.Path);
        }

        public ICacheEntry InvalidateEntry(CoordinationEntryPath path)
        {
            var entry = _cache.GetOrAdd(path, _ => new CacheEntry(path));
            entry.Invalidate();
            return entry;
        }

        public ICacheEntry UpdateEntry(ICacheEntry cacheEntry, IStoredEntry entry)
        {
            if (cacheEntry == null)
                throw new ArgumentNullException(nameof(cacheEntry));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (cacheEntry.Path != entry.Path)
            {
                throw new ArgumentException("The paths of the specified cache entry and stored entry do not match.");
            }

            return UpdateEntry(entry, cacheEntry.CacheEntryVersion);

        }

        public ICacheEntry UpdateEntry(IStoredEntry entry, int comparandVersion)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var path = entry.Path;

            do
            {
                if (!_cache.TryGetValue(path, out var current))
                {
                    current = null;
                }

                var start = current;

                if (start == null)
                {
                    var desired = new CacheEntry(entry.Path, entry);

                    if (_cache.TryAdd(path, desired))
                    {
                        return desired;
                    }
                }
                else
                {
                    var desired = start.Update(entry, comparandVersion);

                    // Nothing to change in the cache. We are done.
                    if (start == desired)
                    {
                        return desired;
                    }

                    if (_cache.TryUpdate(path, desired, start))
                    {
                        return desired;
                    }
                }
            }
            while (true);
        }

        public ICacheEntry RemoveEntry(CoordinationEntryPath path)
        {
            if (!_cache.TryRemove(path, out var result))
            {
                result = null;
            }
            return result;
        }

        private sealed class CacheEntry : ICacheEntry
        {
            public CacheEntry(CoordinationEntryPath path)
            {
                Assert(path != null);

                Path = path;
                Entry = default;
                CacheEntryVersion = 1;
                IsValid = false;
                LocalLock = new AsyncLock();
            }

            public CacheEntry(CoordinationEntryPath path, IStoredEntry entry)
            {
                Assert(path != null);
                Assert(entry != null);

                Path = path;
                Entry = entry;
                CacheEntryVersion = 1;
                IsValid = true;
                LocalLock = new AsyncLock();
            }

            private CacheEntry(CoordinationEntryPath path, IStoredEntry entry, bool isValid, int version, AsyncLock localLock)
            {
                Assert(path != null);
                Assert(isValid, entry != null);
                Assert(localLock != null);

                Path = path;
                Entry = entry;
                CacheEntryVersion = version;
                IsValid = isValid;
                LocalLock = localLock;
            }

            public CoordinationEntryPath Path { get; }

            public bool IsValid { get; }

            public IStoredEntry Entry { get; }

            // The cache entry version's purpose is to prevent a situation that
            // 1) a read operation tries to update the cache and
            // 2) another session concurrently writes to the entry, invalidating our cache entry
            // Without the version, the cache update of operation (1) and the cache invalidation of operation (2)
            // may be performed in the wrong order, leaving the cache with a non-invalidated entry but no read-lock aquired.
            // For this reason, each invalidation increments the version by one, each update operation checks the version.
            public int CacheEntryVersion { get; }

            public AsyncLock LocalLock { get; }

            public CacheEntry Invalidate()
            {
                return new CacheEntry(Path, null, isValid: false, CacheEntryVersion + 1, LocalLock);
            }

            public CacheEntry Update(IStoredEntry entry, int version)
            {
                if (version != CacheEntryVersion ||
                    entry == null ||
                    IsValid && Entry != null && Entry.StorageVersion > entry.StorageVersion)
                {
                    return this;
                }

                return new CacheEntry(Path, entry, isValid: true, version, LocalLock);
            }
        }
    }
}
