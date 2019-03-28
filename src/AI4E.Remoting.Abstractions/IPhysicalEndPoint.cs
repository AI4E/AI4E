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

namespace AI4E.Remoting
{
    /// <summary>
    /// Represents a physical end-point that is able to receive and send messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end-point neither does guarantee message delivery
    /// nor does it provide any guarantees about the ordering of messages.
    /// </remarks>
    public interface IPhysicalEndPoint<TAddress>
        : IInboundPhysicalEndPoint<TAddress>, IOutboundPhysicalEndPoint<TAddress>, IDisposable
    {
        /// <summary>
        /// Gets the physical address of the local physical end point.
        /// </summary>
        new TAddress LocalAddress { get; }
    }

    /// <summary>
    /// Represents an inbound physical end-point that is able to receive messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end-point neither does guarantee message delivery
    /// nor does it provide any guarantees about the ordering of messages.
    /// </remarks>
    public interface IInboundPhysicalEndPoint<TAddress> : IDisposable
    {
        /// <summary>
        /// Asynchronously receives a message from the physical end-point.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the received message
        /// and the address of the remote physical end-point.
        /// </returns>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the asynchronous operation was canceled.
        /// </exception>
        Task<(IMessage message, TAddress remoteAddress)> ReceiveAsync(CancellationToken cancellation = default);
        // TODO: Return ValueTask<(IMessage message, TAddress remoteAddress)>

        /// <summary>
        /// Gets the physical address of the local physical end-point.
        /// </summary>
        TAddress LocalAddress { get; }
    }

    /// <summary>
    /// Represents an outbound physical end-point that is able to send messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end point neither does guarantee message delivery
    /// nor does it provide any guarantees about the ordering of messages.
    /// </remarks>
    public interface IOutboundPhysicalEndPoint<TAddress> : IDisposable
    {
        /// <summary>
        /// Asynchronously send a message to the remote physical end-point with the specified address.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="remoteAddress">The address of the remote physical end-point.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="remoteAddress"/> is null.
        /// </exception>
        /// <exception cref="ArgumentDefaultException">
        /// Thrown if <paramref name="remoteAddress"/> is the default value of type <typeparamref name="TAddress"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if the asynchronous operation was canceled.
        /// </exception>
        Task SendAsync( IMessage message, TAddress remoteAddress, CancellationToken cancellation = default);
        // TODO: Return ValueTask

        /// <summary>
        /// Gets the physical address of the local physical end-point.
        /// </summary>
        TAddress LocalAddress { get; }
    }
}
