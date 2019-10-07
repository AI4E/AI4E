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
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Remoting
{
    /// <summary>
    /// Represents a physical end-point that is able to receive and send messages.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end-point neither does guarantee message delivery nor does it provide any guarantees about the ordering of messages.
    /// </remarks>
    public interface IPhysicalEndPoint<TAddress> : IAddressConverter<TAddress>, IDisposable
    {
        /// <summary>
        /// Gets the physical address of the local physical end point.
        /// </summary>
        TAddress LocalAddress { get; }

        /// <summary>
        /// Asynchronously receives a message from the physical end-point.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the received message and the address of the remote physical end-point.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        ValueTask<Transmission<TAddress>> ReceiveAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously send a message to the remote physical end-point with the specified address.
        /// </summary>
        /// <param name="transmission">The message transmisstion.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="transmission"/> is <c>default</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        ValueTask SendAsync(Transmission<TAddress> transmission, CancellationToken cancellation = default);
    }

    public interface IAddressConverter<TAddress>
    {
        /// <summary>
        /// Returns a string representation of the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <returns>The string representation of <paramref name="address"/>.</returns>
        string AddressToString(TAddress address);

        /// <summary>
        /// Returns the address that is represented by the specified string.
        /// </summary>
        /// <param name="str">The string representing the address.</param>
        /// <returns>The address that is represented by <paramref name="str"/>.</returns>
        TAddress AddressFromString(string str);
    }
}
