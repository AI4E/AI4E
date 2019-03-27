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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Session
{
    /// <summary>
    /// Manages coordination service sessions.
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Asynchronously adds an entry to the specified coordination service session. 
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="entryPath">
        /// A <see cref="CoordinationEntryPath"/> that specifies the entry to add.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task AddSessionEntryAsync(
            CoordinationSession session, CoordinationEntryPath entryPath, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously removed an entry from the specified coordination service session. 
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="entryPath">
        /// A <see cref="CoordinationEntryPath"/> that specifies the entry to remove.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task RemoveSessionEntryAsync(
            CoordinationSession session, CoordinationEntryPath entryPath, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously retrieved a collection of entries of the specified coordination service session.
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains a collection of entries of <paramref name="session"/>.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task<IEnumerable<CoordinationEntryPath>> GetEntriesAsync(
            CoordinationSession session, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously tries to start a coordination service session with the specified identifier.
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="leaseEnd">
        /// A <see cref="DateTime"/> that specifies the point in time the session expires.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the operation was successful.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task<bool> TryBeginSessionAsync(
            CoordinationSession session, DateTime leaseEnd, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously extends the lifetime of a coordination service session with the specified identifier.
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="leaseEnd">
        /// A <see cref="DateTime"/> that specifies the point in time the session expires.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="SessionTerminatedException">
        /// Thrown if the session specified by <paramref name="session"/> is terminated.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task UpdateSessionAsync( // TODO: Rename (ExtendLifetime), Use a boolean return value to indicate session termination
            CoordinationSession session, DateTime leaseEnd, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously terminates the coordination service session with the specified identifier.
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task EndSessionAsync(CoordinationSession session, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously awaits the specified coordination service session to terminate.
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task WaitForTerminationAsync(CoordinationSession session, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously awaits a coordination service session to terminate
        /// and provides the terminated sessions identifier.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the identifier of the the terminated session.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task<CoordinationSession> WaitForTerminationAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously retrivies a boolean value indicating whether the specified
        /// coordination service session is alive.
        /// </summary>
        /// <param name="session">
        /// A <see cref="CoordinationSession"/> that identifies the coordination entry session.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the
        /// coordination service session specified by <paramref name="session"/> is alive.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="session"/> is the default value of <see cref="CoordinationSession"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task<bool> IsAliveAsync(CoordinationSession session, CancellationToken cancellation = default); // TODO: Negate this?

        /// <summary>
        /// Asynchronously retrieves a collection of all known coordination service sessions.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that enumerates all known coordination service sessions.
        /// </returns>
        IAsyncEnumerable<CoordinationSession> GetSessionsAsync(CancellationToken cancellation = default);
    }
}
