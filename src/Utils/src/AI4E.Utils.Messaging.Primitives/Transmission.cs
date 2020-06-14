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

namespace AI4E.Utils.Messaging.Primitives
{
    /// <summary>
    /// Represents a message transmission.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used.</typeparam>
    public readonly struct Transmission<TAddress> : IEquatable<Transmission<TAddress>>
    {
        /// <summary>
        /// Creates a new value of the <see cref="Transmission{TAddress}"/> type.
        /// </summary>
        /// <param name="message">The transmit message.</param>
        /// <param name="remoteAddress">The address of the remote physical end-point.</param>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="remoteAddress"/> is <c>default</c>.</exception>
        public Transmission(Message message, TAddress remoteAddress)
        {
            if (EqualityComparer<TAddress>.Default.Equals(remoteAddress, default!))
            {
                this = default;
                return;
            }

            Message = message;
            RemoteAddress = remoteAddress;
        }

        /// <summary>
        /// Gets the transmit message.
        /// </summary>
        public Message Message { get; }

        /// <summary>
        /// Gets the address of the remote physical end-point.
        /// </summary>
        public TAddress RemoteAddress { get; }

        public bool Equals(Transmission<TAddress> other)
        {
            return Message.Equals(other.Message) &&
                   EqualityComparer<TAddress>.Default.Equals(RemoteAddress, other.RemoteAddress);
        }

        public override bool Equals(object? obj)
        {
            return obj is Transmission<TAddress> transmission && Equals(transmission);
        }

        public override int GetHashCode()
        {
            return (Message, RemoteAddress).GetHashCode();
        }

        public static bool operator ==(Transmission<TAddress> left, Transmission<TAddress> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Transmission<TAddress> left, Transmission<TAddress> right)
        {
            return !left.Equals(right);
        }
    }
}
