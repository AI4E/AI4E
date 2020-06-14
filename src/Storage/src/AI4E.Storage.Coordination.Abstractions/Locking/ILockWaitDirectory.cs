/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Coordination.Session;

namespace AI4E.Storage.Coordination.Locking
{
    /// <summary>
    /// A directory that can be used to wait for lock releases.
    /// </summary>
    public interface ILockWaitDirectory
    {
        /// <summary>
        /// Notifies that a read-lock was released.
        /// </summary>
        /// <param name="key">The key of the entry that a read-lock was released for.</param>
        /// <param name="session">The session that formerly owned the read-lock.</param>
        void NotifyReadLockRelease(
            string key,
            SessionIdentifier session);

        /// <summary>
        /// Notifies that a write-lock was released.
        /// </summary>
        /// <param name="key">The key of the entry that a write-lock was released for.</param>
        /// <param name="session">The session that formerly owned the write-lock.</param>
        void NotifyWriteLockRelease(
            string key,
            SessionIdentifier session);

        /// <summary>
        /// Asynchronously awaits the release of a read-lock.
        /// </summary>
        /// <param name="key">The key of the entry that a read-lock is owned.</param>
        /// <param name="session">The session that owns the read-lock.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask WaitForReadLockNotificationAsync(
            string key,
            SessionIdentifier session,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously awaits the release of a write-lock.
        /// </summary>
        /// <param name="key">The key of the entry that a write-lock is owned.</param>
        /// <param name="session">The session that owns the write-lock.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask WaitForWriteLockNotificationAsync(
            string key,
            SessionIdentifier session,
            CancellationToken cancellation = default);
    }
}
