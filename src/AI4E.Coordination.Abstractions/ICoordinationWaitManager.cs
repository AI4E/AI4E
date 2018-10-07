using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination
{
    public interface ICoordinationWaitManager
    {
        void NotifyReadLockRelease(CoordinationEntryPath path, Session session);
        void NotifyWriteLockRelease(CoordinationEntryPath path, Session session);

        Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, bool allowWriteLock, CancellationToken cancellation);
        Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation);
    }
}
