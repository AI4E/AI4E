using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Coordination.Storage;

namespace AI4E.Storage.Coordination.Locking
{
    public interface ICoordinationWaitManager
    {
        ValueTask<IStoredEntry> WaitForWriteLockReleaseAsync(
            IStoredEntry entry,
            bool allowWriteLock,
            CancellationToken cancellation);

        ValueTask<IStoredEntry> WaitForReadLocksReleaseAsync(
            IStoredEntry entry,
            CancellationToken cancellation);
    }
}
