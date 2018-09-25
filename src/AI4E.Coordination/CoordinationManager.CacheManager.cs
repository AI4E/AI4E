using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;

// TODO: Is this type worth it?

namespace AI4E.Coordination
{
    public sealed partial class CoordinationManager<TAddress>
    {
        private sealed class CacheManager : ICoordinationCacheManager
        {
            private readonly CoordinationEntryCache _cache;
            private readonly ICoordinationLockManager _lockManager;

            public CacheManager(CoordinationEntryCache cache, ICoordinationLockManager lockManager)
            {
                if (cache == null)
                    throw new ArgumentNullException(nameof(cache));

                if (lockManager == null)
                    throw new ArgumentNullException(nameof(lockManager));

                _cache = cache;
                _lockManager = lockManager;
            }

            public bool TryGetCacheEntry(CoordinationEntryPath path, out CacheEntry cacheEntry)
            {
                return _cache.TryGetEntry(path, out cacheEntry);
            }

            public void UpdateCacheEntry(CacheEntry cacheEntry, IStoredEntry entry)
            {
                if (cacheEntry == null)
                    throw new ArgumentNullException(nameof(cacheEntry));

                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                _cache.UpdateEntry(cacheEntry, entry);
            }

            public async Task<IStoredEntry> AddToCacheAsync(IStoredEntry entry, CancellationToken cancellation)
            {
                if (entry == null)
                    throw new ArgumentNullException(nameof(entry));

                var path = entry.Path;

                try
                {
                    entry = await _lockManager.AcquireReadLockAsync(entry, cancellation);

                    if (entry != null)
                    {
                        _cache.UpdateEntry(entry, comparandVersion: default);
                    }

                    return entry;
                }
                catch
                {
                    await _lockManager.ReleaseReadLockAsync(path, cancellation);

                    throw;
                }
            }

            public async Task InvalidateCacheEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
            {
                _cache.InvalidateEntry(path);
                await _lockManager.ReleaseReadLockAsync(path, cancellation);
            }
        }
    }
}
