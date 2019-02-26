using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Locking
{
    public interface ILockWaitDirectory
    {
        void NotifyReadLockRelease(
            string key,
            CoordinationSession session);

        void NotifyWriteLockRelease(
            string key,
            CoordinationSession session);

        ValueTask WaitForReadLockNotificationAsync(
            string key,
            CoordinationSession session,
            CancellationToken cancellation = default);

        ValueTask WaitForWriteLockNotificationAsync(
            string key,
            CoordinationSession session,
            CancellationToken cancellation = default);
    }
}
