using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Utils;

namespace AI4E.Coordination.Locking
{
    public sealed class LockWaitDirectory : ILockWaitDirectory
    {
        private readonly AsyncWaitDirectory<(string key, CoordinationSession session)> _readLockWaitDirectory;
        private readonly AsyncWaitDirectory<(string key, CoordinationSession session)> _writeLockWaitDirectory;

        public LockWaitDirectory()
        {
            _readLockWaitDirectory = new AsyncWaitDirectory<(string key, CoordinationSession session)>();
            _writeLockWaitDirectory = new AsyncWaitDirectory<(string key, CoordinationSession session)>();
        }

        public void NotifyReadLockRelease(
            string key,
            CoordinationSession session)
        {
            _readLockWaitDirectory.Notify((key, session));
        }

        public void NotifyWriteLockRelease(
            string key,
            CoordinationSession session)
        {
            _writeLockWaitDirectory.Notify((key, session));
        }

        public ValueTask WaitForReadLockNotificationAsync(
            string key,
            CoordinationSession session,
            CancellationToken cancellation)
        {
            return _readLockWaitDirectory.WaitForNotificationAsync((key, session), cancellation).AsValueTask();
        }

        public ValueTask WaitForWriteLockNotificationAsync(
            string key,
            CoordinationSession session,
            CancellationToken cancellation)
        {
            return _writeLockWaitDirectory.WaitForNotificationAsync((key, session), cancellation).AsValueTask();
        }
    }
}
