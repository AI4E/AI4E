using System.Threading;
using System.Threading.Tasks;

// TODO: Is this type worth it?

namespace AI4E.Coordination
{
    internal interface ICoordinationCacheManager
    {
        bool TryGetCacheEntry(CoordinationEntryPath path, out CacheEntry cacheEntry);
        void UpdateCacheEntry(CacheEntry cacheEntry, IStoredEntry entry);
        Task<IStoredEntry> AddToCacheAsync(IStoredEntry entry, CancellationToken cancellation);
        Task InvalidateCacheEntryAsync(CoordinationEntryPath path, CancellationToken cancellation);
    }
}
