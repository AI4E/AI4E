using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Locking
{
    public interface ILockWaitDirectory
    {
        void NotifyReadLockRelease(CoordinationEntryPath path, CoordinationSession session);
        void NotifyWriteLockRelease(CoordinationEntryPath path, CoordinationSession session);
        Task WaitForReadLockNotificationAsync(CoordinationEntryPath path, CoordinationSession session, CancellationToken cancellation = default);
        Task WaitForWriteLockNotificationAsync(CoordinationEntryPath path, CoordinationSession session, CancellationToken cancellation = default);
    }
}
