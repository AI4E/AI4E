using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Utils;

namespace AI4E.Coordination.Locking
{
    public sealed class LockWaitDirectory : ILockWaitDirectory
    {
        private readonly AsyncWaitDirectory<(CoordinationEntryPath path, CoordinationSession session)> _readLockWaitDirectory;
        private readonly AsyncWaitDirectory<(CoordinationEntryPath path, CoordinationSession session)> _writeLockWaitDirectory;

        public LockWaitDirectory()
        {
            _readLockWaitDirectory = new AsyncWaitDirectory<(CoordinationEntryPath path, CoordinationSession session)>();
            _writeLockWaitDirectory = new AsyncWaitDirectory<(CoordinationEntryPath path, CoordinationSession session)>();
        }

        public void NotifyReadLockRelease(CoordinationEntryPath path, CoordinationSession session)
        {
            _readLockWaitDirectory.Notify((path, session));
        }

        public void NotifyWriteLockRelease(CoordinationEntryPath path, CoordinationSession session)
        {
            _writeLockWaitDirectory.Notify((path, session));
        }

        public Task WaitForReadLockNotificationAsync(CoordinationEntryPath path, CoordinationSession session, CancellationToken cancellation = default)
        {
            return _readLockWaitDirectory.WaitForNotificationAsync((path, session), cancellation);
        }

        public Task WaitForWriteLockNotificationAsync(CoordinationEntryPath path, CoordinationSession session, CancellationToken cancellation = default)
        {
            return _writeLockWaitDirectory.WaitForNotificationAsync((path, session), cancellation);
        }
    }
}
