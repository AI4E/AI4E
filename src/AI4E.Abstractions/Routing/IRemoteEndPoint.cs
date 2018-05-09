/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IRemoteEndPoint.cs 
 * Types:           (1) AI4E.Routing.IRemoteEndPoint
 *                  (2) AI4E.Routing.IRemoteEndPoint'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.05.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Remoting;

namespace AI4E.Routing
{
    /// <summary>
    /// Represents a remote logical end point that messages can be sent to.
    /// </summary>
    /// <remarks>
    /// This type is not meant to be consumed directly but is part of the infrastructure to enable the remote message dispatching system.
    /// </remarks>
    public interface IRemoteEndPoint : IDisposable
    {
        /// <summary>
        /// Gets the route of the remote virtual end point.
        /// </summary>
        EndPointRoute Route { get; }

        /// <summary>
        /// Asynchronously sends a message to the remote virtual end point.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="localEndPoint">The route of the local virtual end point.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="message"/> or <paramref name="localEndPoint"/> is null. </exception>
        /// <exception cref="OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        Task SendAsync(IMessage message, EndPointRoute localEndPoint, CancellationToken cancellation);
    }

    /// <summary>
    /// Represents a remote logical end point that messages can be sent to.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used in the protocol stack.</typeparam>
    /// <remarks>
    /// This type is not meant to be consumed directly but is part of the infrastructure to enable the remote message dispatching system.
    /// </remarks>
    public interface IRemoteEndPoint<TAddress> : IRemoteEndPoint
    {
        /// <summary>
        /// Gets the physical address of the local physical end point.
        /// </summary>
        TAddress LocalAddress { get; }

        /// <summary>
        /// Asynchronously sends a message the replication of the remote virtual end point with the specified address.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="localEndPoint">The route of the local virtual end point.</param>
        /// <param name="remoteAddress">The physical address of the replication to send the message to.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any of <paramref name="message"/>, <paramref name="localEndPoint"/> or <paramref name="remoteAddress"/> is null. </exception>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="remoteAddress"/> is the default value of type <see cref="TAddress"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        Task SendAsync(IMessage message, EndPointRoute localEndPoint, TAddress remoteAddress, CancellationToken cancellation);
    }
}
