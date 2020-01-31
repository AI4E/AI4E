using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

namespace AI4E.Storage.Coordination
{
    public sealed class LockWaitDirectory : ILockWaitDirectory
    {
        private readonly AsyncWaitDirectory<(CoordinationEntryPath path, Session session)> _readLockWaitDirectory;
        private readonly AsyncWaitDirectory<(CoordinationEntryPath path, Session session)> _writeLockWaitDirectory;

        public LockWaitDirectory()
        {
            _readLockWaitDirectory = new AsyncWaitDirectory<(CoordinationEntryPath path, Session session)>();
            _writeLockWaitDirectory = new AsyncWaitDirectory<(CoordinationEntryPath path, Session session)>();
        }

        public void NotifyReadLockRelease(CoordinationEntryPath path, Session session)
        {
            _readLockWaitDirectory.Notify((path, session));
        }

        public void NotifyWriteLockRelease(CoordinationEntryPath path, Session session)
        {
            _writeLockWaitDirectory.Notify((path, session));
        }

        public Task WaitForReadLockNotificationAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation = default)
        {
            return _readLockWaitDirectory.WaitForNotificationAsync((path, session), cancellation);
        }

        public Task WaitForWriteLockNotificationAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation = default)
        {
            return _writeLockWaitDirectory.WaitForNotificationAsync((path, session), cancellation);
        }
    }
}
