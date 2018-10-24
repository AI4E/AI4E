using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination
{
    public interface ILockWaitDirectory
    {
        void NotifyReadLockRelease(CoordinationEntryPath path, Session session);
        void NotifyWriteLockRelease(CoordinationEntryPath path, Session session);
        Task WaitForReadLockNotificationAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation = default);
        Task WaitForWriteLockNotificationAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation = default);
    }
}
