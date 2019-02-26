using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Caching
{
    public interface ICoordinationCacheManager
    {
        ValueTask<ICacheEntry> GetCacheEntryAsync(string key, CancellationToken cancellation);
    }
}
