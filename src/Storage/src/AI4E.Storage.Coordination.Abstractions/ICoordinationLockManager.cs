using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Coordination
{
    public interface ICoordinationLockManager
    {
        Task AcquireLocalWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation);
        Task ReleaseLocalWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation);

        Task AcquireLocalReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation);
        Task ReleaseLocalReadLockAsync(CoordinationEntryPath path, CancellationToken cancellation);

        // Acquired a read lock for the entry with the specified path and returns the entry.
        // If the result is null, the entry does not exist and no lock is allocated.
        Task<IStoredEntry> AcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation);

        // Releases the write lock for the specified entry and returns the updated entry.
        // If the current session does not own the write-lock for the entry (f.e. if it is deleted), 
        // this method only releases the local lock but is a no-op otherwise.
        Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry, CancellationToken cancellation);

        Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation);
        Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry, CancellationToken cancellation);
    }
}
