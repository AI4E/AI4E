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
using System.Threading;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Coordination
{
    /// <summary>
    /// The datastructure the coordination service uses to cache entries.
    /// </summary>
    /// <remarks>This type is thread-safe.</remarks>
    public sealed class CoordinationEntryCache
    {
        private readonly ConcurrentDictionary<CoordinationEntryPath, CacheEntry> _cache;

        /// <summary>
        /// Creates a new instance of the <see cref="CoordinationEntryCache"/> type.
        /// </summary>
        public CoordinationEntryCache()
        {
            _cache = new ConcurrentDictionary<CoordinationEntryPath, CacheEntry>();
        }

        /// <summary>
        /// Tries to get the cache entry with the specified path.
        /// </summary>
        /// <param name="path">The path of the cache entry.</param>
        /// <param name="cacheEntry">Contains the cache entry if the operation succeeds.</param>
        /// <returns>True if the cache entry can be read from the cache, false otherwise.</returns>
        public bool TryGetEntry(CoordinationEntryPath path, out CacheEntry cacheEntry)
        {
            if (_cache.TryGetValue(path, out var result))
            {
                cacheEntry = result;
                return true;
            }

            cacheEntry = null;
            return false;
        }

        /// <summary>
        /// Gets the cache entry with the specified path.
        /// </summary>
        /// <param name="path">The path of the cache entry.</param>
        /// <returns>The cache entry.</returns>
        /// <remarks>
        /// This operation does always return a cache entry and never returns null.
        /// If no cache entry with the specifies path can be found in the cache, 
        /// an invalidated entry is inserted.
        /// </remarks>
        public CacheEntry GetEntry(CoordinationEntryPath path)
        {
            return _cache.GetOrAdd(path, _ => new CacheEntry(path));
        }

        /// <summary>
        /// Invalidates the specified cache entry.
        /// </summary>
        /// <param name="cacheEntry">The cache entry to invalidate.</param>
        /// <returns>The invalidated cache entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cacheEntry"/> is null.</exception>
        public CacheEntry InvalidateEntry(CacheEntry cacheEntry)
        {
            if (cacheEntry == null)
                throw new ArgumentNullException(nameof(cacheEntry));

            return InvalidateEntry(cacheEntry.Path);
        }

        /// <summary>
        /// Invalidates the cache entry with the specified path.
        /// </summary>
        /// <param name="path">The path of the cache entry.</param>
        /// <returns>The invalidated cache entry.</returns>
        public CacheEntry InvalidateEntry(CoordinationEntryPath path)
        {
            return _cache.AddOrUpdate(path, _ => new CacheEntry(path), (_, e) => e.Invalidate());
        }

        /// <summary>
        /// Updates the cache entry with the specified stored entry.
        /// </summary>
        /// <param name="cacheEntry">The cache entry to update.</param>
        /// <param name="entry">The stored entry.</param>
        /// <returns>The updated cache entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="cacheEntry"/> or <paramref name="entry"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="cacheEntry"/> and <paramref name="entry"/> have different paths.</exception>
        /// <remarks>
        /// It is NOT guaranteed that the returned cache entry is valid and the <see cref="CacheEntry.Entry"/> property accessable.
        /// </remarks>
        public CacheEntry UpdateEntry(CacheEntry cacheEntry, IStoredEntry entry)
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

        public CacheEntry AddEntry(IStoredEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return UpdateEntry(entry, comparandVersion: default);
        }

        /// <summary>
        /// Updates the cache entry with the specified stored entry if the version matches.
        /// </summary>
        /// <param name="entry">The stored entry.</param>
        /// <param name="comparandVersion">The cache entry comparand version.</param>
        /// <returns>The updated cache entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is null.</exception>
        /// <remarks>
        /// It is NOT guaranteed that the returned cache entry is valid and the <see cref="CacheEntry.Entry"/> property accessable.
        /// </remarks>
        private CacheEntry UpdateEntry(IStoredEntry entry, int comparandVersion)
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

        /// <summary>
        /// Removed the cache entry with the specified path from the cache.
        /// </summary>
        /// <param name="path">The path of the cache entry.</param>
        /// <returns>The removed cache entry or null if the cache does not contain a cache entry with the specified path.</returns>
        public CacheEntry RemoveEntry(CoordinationEntryPath path)
        {
            if (!_cache.TryRemove(path, out var result))
            {
                result = null;
            }
            return result;
        }
    }

    /// <summary> Represent a cache entry. </summary>
    /// <remarks> This type is thread-safe. </remarks>
    public sealed class CacheEntry
    {
        private readonly IStoredEntry _entry;

        internal CacheEntry(CoordinationEntryPath path)
        {
            Assert(path != null);

            Path = path;
            _entry = null;
            CacheEntryVersion = 1;
            LocalReadLock = CreateLocalLock();
            LocalWriteLock = CreateLocalLock();
        }

        internal CacheEntry(CoordinationEntryPath path, IStoredEntry entry)
        {
            Assert(path != null);
            Assert(entry != null);

            Path = path;
            _entry = entry;
            CacheEntryVersion = 1;
            LocalReadLock = CreateLocalLock();
            LocalWriteLock = CreateLocalLock();
        }

        private CacheEntry(CoordinationEntryPath path,
                           IStoredEntry entry,
                           int version,
                           SemaphoreSlim localReadLock,
                           SemaphoreSlim localWriteLock)
        {
            Assert(path != null);
            Assert(localReadLock != null);
            Assert(localWriteLock != null);

            Path = path;
            _entry = entry;
            CacheEntryVersion = version;
            LocalReadLock = localReadLock;
            LocalWriteLock = localWriteLock;
        }

        private static SemaphoreSlim CreateLocalLock()
        {
            return new SemaphoreSlim(1);
        }

        /// <summary>
        /// The path of the entry, the cache entry stores.
        /// </summary>
        public CoordinationEntryPath Path { get; }

        /// <summary>
        /// A boolean value indicating whether the cache entry is valid and <see cref="Entry"/> can be used safely.
        /// </summary>
        public bool IsValid => _entry != null;

        /// <summary>
        /// The stored entry, the cache entry stored.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the cache entry is invalidated.</exception>
        public IStoredEntry Entry
        {
            get
            {
                if (!IsValid)
                {
                    throw new InvalidOperationException();
                }

                return _entry;
            }
        }

        /// <summary>
        /// Gets the local read lock of the cache entry.
        /// </summary>
        public SemaphoreSlim LocalReadLock { get; }

        /// <summary>
        /// Gets the local write lock of the cache entry.
        /// </summary>
        public SemaphoreSlim LocalWriteLock { get; }

        // The cache entry version's purpose is to prevent a situation that
        // 1) a read operation tries to update the cache and
        // 2) another session concurrently writes to the entry, invalidating our cache entry
        // Without the version, the cache update of operation (1) and the cache invalidation of operation (2)
        // may be performed out of order, leaving the cache with a non-invalidated entry but no read-lock aquired.
        // For this reason, each invalidation increments the version by one, each update operation checks the version.
        internal int CacheEntryVersion { get; }

        public bool TryGetEntry(out IStoredEntry entry)
        {
            entry = _entry;
            return IsValid;
        }

        internal CacheEntry Invalidate()
        {
            return new CacheEntry(Path, entry: null, version: CacheEntryVersion + 1, localReadLock: LocalReadLock, localWriteLock: LocalWriteLock);
        }

        internal CacheEntry Update(IStoredEntry entry, int version)
        {
            if (version != CacheEntryVersion ||
                entry == null ||
                IsValid && Entry != null && Entry.StorageVersion > entry.StorageVersion)
            {
                return this;
            }

            return new CacheEntry(Path, entry, version: version, localReadLock: LocalReadLock, localWriteLock: LocalWriteLock);
        }
    }
}
