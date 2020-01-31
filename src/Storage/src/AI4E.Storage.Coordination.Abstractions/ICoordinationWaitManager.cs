using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination
{
    public interface ICoordinationWaitManager
    {
        Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, bool allowWriteLock, CancellationToken cancellation);
        Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation);
    }
}
