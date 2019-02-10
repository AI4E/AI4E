using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Storage;

namespace AI4E.Coordination.Locking
{
    public interface ICoordinationWaitManager
    {
        Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, bool allowWriteLock, CancellationToken cancellation);
        Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation);
    }
}
