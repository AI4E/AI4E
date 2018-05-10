/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IPhysicalEndPoint.cs 
 * Types:           (1) AI4E.Remoting.IPhysicalEndPoint'1
 *                  (2) AI4E.Remoting.IInboundPhysicalEndPoint'1
 *                  (3) AI4E.Remoting.IOutboundPhysicalEndPoint'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   10.05.2018 
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

using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Remoting
{
    /// <summary>
    /// Represents a physical end point that is able to receive and send messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end point neither does guarantee message delivery nor does it provide any guarantiees about the ordering of messages.
    /// </remarks>
    public interface IPhysicalEndPoint<TAddress> : IInboundPhysicalEndPoint<TAddress>, IOutboundPhysicalEndPoint<TAddress>
    {
        /// <summary>
        /// Gets the physical address of the local phyiscal end point.
        /// </summary>
        new TAddress LocalAddress { get; }
    }

    /// <summary>
    /// Represents an inbound physical end point that is able to receive messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end point neither does guarantee message delivery nor does it provide any guarantiees about the ordering of messages.
    /// </remarks>
    public interface IInboundPhysicalEndPoint<TAddress>
    {
        /// <summary>
        /// Asynchronously receives a message from the physical end point.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the received message.
        /// </returns>
        /// <exception cref="System.OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        Task<IMessage> ReceiveAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Gets the physical address of the local phyiscal end point.
        /// </summary>
        TAddress LocalAddress { get; }
    }

    /// <summary>
    /// Represents an outbound physical end point that is able to send messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end point neither does guarantee message delivery nor does it provide any guarantiees about the ordering of messages.
    /// </remarks>
    public interface IOutboundPhysicalEndPoint<TAddress>
    {
        /// <summary>
        /// Asynchronously send a message to the remote end point with the specifies address.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="address">The address of the remote end point.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if either <paramref name="message"/> or <paramref name="address"/> is null.</exception>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="address"/> is the default value of type <see cref="TAddress"/>.</exception>
        /// <exception cref="System.OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        Task SendAsync(IMessage message, TAddress address, CancellationToken cancellation = default);

        /// <summary>
        /// Gets the physical address of the local phyiscal end point.
        /// </summary>
        TAddress LocalAddress { get; }
    }
}
