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

namespace AI4E.Remoting
{
    /// <summary>
    /// Multiplexes a single physical end point to multiple end points, each distinguished by a multiplex name.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    public interface IPhysicalEndPointMultiplexer<TAddress> : IDisposable
    {
        /// <summary>
        /// Returns a physical end point that is identified by the specified multiplex name.
        /// </summary>
        /// <param name="multiplexName">The name of the multiplex end point.</param>
        /// <returns>A physical end point identified by <paramref name="multiplexName"/>.</returns>
        /// <exception cref="ArgumentNullOrWhiteSpaceException">
        /// Thrown if <paramref name="multiplexName"/> is either null, an empty string or contains of whitespace only.
        /// </exception>
        IMultiplexPhysicalEndPoint<TAddress> GetPhysicalEndPoint(string multiplexName);

        /// <summary>
        /// Gets the physical address of the underlying local physical end point.
        /// </summary>
        TAddress LocalAddress { get; }
    }

    /// <summary>
    /// Represents a multiplex physical end point.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    /// <remarks>
    /// The physical end point neither does guarantee message delivery
    /// nor does it provide any guarantees about the ordering of messages.
    /// </remarks>
    public interface IMultiplexPhysicalEndPoint<TAddress> : IPhysicalEndPoint<TAddress>
    {
        /// <summary>
        /// Gets the multiplex name that identifies the end point.
        /// </summary>
        string MultiplexName { get; }
    }
}
