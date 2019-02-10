using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Storage;

namespace AI4E.Coordination.Locking
{
    public interface ICoordinationLockManager
    {
        Task<IStoredEntry> AcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation);
        Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry);
        Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation);
        Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry);
    }
}
