using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Storage;

namespace AI4E.Coordination.Caching
{
    public interface ICoordinationCacheManager
    {
        ValueTask<IStoredEntry> GetEntryAsync(CoordinationEntryPath path, CancellationToken cancellation);
        ValueTask InvalidateEntryAsync(CoordinationEntryPath path, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously tries to acquire a lock for the entry with the specified path and creates it locally if the entry does not exist.
        /// </summary>
        /// <param name="path">The path specifying the entry.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entry if it was locked successfully or null if it was created locally.
        /// </returns>
        /// <remarks>
        /// In both cases, resources are allocated for the entry that the caller need to free by a call to <see cref="UnlockEntryAsync(CoordinationEntryPath)"/>.
        /// </remarks>
        ValueTask<IStoredEntry> LockOrCreateEntryAsync(CoordinationEntryPath path, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously tries to acquire a lock for the entry with the specified path.
        /// </summary>
        /// <param name="path">The path specifying the entry.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entry if it was locked successfully or null if it is not existing.
        /// </returns>
        /// <remarks>
        /// In case, null is returned the entry was not exiting at the time of the call and no resource was allocated. The entry was not locked and does not have to be unlocked.
        /// </remarks>
        ValueTask<IStoredEntry> LockEntryAsync(CoordinationEntryPath path, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously releases the lock for the entry with the specified path.
        /// </summary>
        /// <param name="path">The path specifying the entry.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entry if it was locked successfully or null if it is not existing.
        /// </returns>
        /// <remarks>
        /// If the current session does not own the write-lock for the entry (f.e. if it is deleted), this method only releases local resources but is a no-op otherwise.
        /// </remarks>
        ValueTask<IStoredEntry> UnlockEntryAsync(CoordinationEntryPath path);
    }
}
