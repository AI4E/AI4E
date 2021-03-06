using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Coordination.Storage;

namespace AI4E.Storage.Coordination.Locking
{
    public interface ICoordinationLockManager
    {
        ValueTask<IStoredEntry> AcquireWriteLockAsync(
            string key,
            CancellationToken cancellation);

        ValueTask<IStoredEntry> AcquireWriteLockAsync(
            IStoredEntry entry,
            CancellationToken cancellation);

        ValueTask<IStoredEntry> ReleaseWriteLockAsync(
            IStoredEntry entry);

        ValueTask<IStoredEntry> AcquireReadLockAsync(
            IStoredEntry entry,
            CancellationToken cancellation);

        ValueTask<IStoredEntry> ReleaseReadLockAsync(
            IStoredEntry entry);
    }
}
