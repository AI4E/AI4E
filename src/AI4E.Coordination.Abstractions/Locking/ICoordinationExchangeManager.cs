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

using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Session;
using AI4E.Remoting;

namespace AI4E.Coordination.Locking
{
    /// <summary>
    /// Manages the communication of coordination service sessions.
    /// </summary>
    public interface ICoordinationExchangeManager : IDisposable
    {
        /// <summary>
        /// Asynchronously notifies all coordination service sessions that
        /// a read-lock was released.
        /// </summary>
        /// <param name="key">The key of the entry the read-lock was released of.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        ValueTask NotifyReadLockReleasedAsync(
            string key,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously notifies all coordination service sessions that
        /// a write-lock was released.
        /// </summary>
        /// <param name="key">The key of the entry the write-lock was released of.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        ValueTask NotifyWriteLockReleasedAsync(
            string key,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously notifies the specified coordination service session
        /// to release the read-lock for the specified entry.
        /// </summary>
        /// <param name="key">The key of the entry the write-lock was released of.</param>
        /// <param name="session">The session that owns the read-lock.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous operation.
        /// </returns>
        ValueTask InvalidateCacheEntryAsync( // TODO: Rename to RealeaseReadLockAsync?
            string key,
            SessionIdentifier session,
            CancellationToken cancellation = default);
    }

    /// <summary>
    /// Manages the communication of coordination coordination service sessions..
    /// </summary>
    /// <typeparam name="TAddress">
    /// The type of address the messaging system uses.
    /// </typeparam>
    public interface ICoordinationExchangeManager<TAddress> : ICoordinationExchangeManager
    {
        /// <summary>
        /// Asynchronously retrieved the physical end-point that is used
        /// to communicate with other coordination service sessions.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the physical end-point.
        /// </returns>
        ValueTask<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(
            CancellationToken cancellation = default);
    }
}
