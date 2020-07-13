/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Diagnostics;

namespace AI4E.Messaging.Routing
{
    /// <summary>
    /// Represents the address of a logical end point.
    /// </summary>
    [Serializable]
    public readonly struct RouteEndPointAddress : IEquatable<RouteEndPointAddress>, ISerializable
    {
        public static RouteEndPointAddress UnknownAddress { get; } = default;

        #region C'tor

        public RouteEndPointAddress(string endPoint)
        {
            if (string.IsNullOrWhiteSpace(endPoint))
            {
                this = UnknownAddress;
            }
            else
            {
                Utf8EncodedValue = Encode(endPoint.AsSpan());
            }
        }

        public RouteEndPointAddress(ReadOnlySpan<char> endPoint)
        {
            if (endPoint.IsEmpty || endPoint.IsWhiteSpace())
            {
                this = UnknownAddress;
            }
            else
            {
                Utf8EncodedValue = Encode(endPoint);
            }
        }

        public RouteEndPointAddress(ReadOnlyMemory<byte> utf8EncodedValue)
        {
            // TODO: Do we have to trim?
            Utf8EncodedValue = utf8EncodedValue;
        }

        private RouteEndPointAddress(SerializationInfo info, StreamingContext context)
        {
            var logicalAddress = info.GetString("address-string");

            if (string.IsNullOrWhiteSpace(logicalAddress))
            {
                this = UnknownAddress;
            }
            else
            {
                Utf8EncodedValue = Encode(logicalAddress.AsSpan());
            }
        }

        private static ReadOnlyMemory<byte> Encode(ReadOnlySpan<char> endPoint)
        {
            var chars = endPoint.Trim();
            var byteCount = Encoding.UTF8.GetByteCount(chars);
            var bytes = new byte[byteCount];
            var bytesWritten = Encoding.UTF8.GetBytes(chars, bytes);
            Debug.Assert(bytesWritten == byteCount);
            return bytes;
        }

        #endregion

        public ReadOnlyMemory<byte> Utf8EncodedValue { get; }

        /// <summary>
        /// Returns a boolean value indicating whether the specifies end point address equals the current instance.
        /// </summary>
        /// <param name="other">The end point address to compare to.</param>
        /// <returns>True if <paramref name="other"/> equals the current end point address, false otherwise.</returns>
        public bool Equals(RouteEndPointAddress other)
        {
            return other.Utf8EncodedValue.Span.SequenceEqual(Utf8EncodedValue.Span);
        }

        /// <summary>
        /// Return a boolean value indicating whether the specifies object equals the current end point address.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>
        /// True if <paramref name="obj"/> is of type <see cref="RouteEndPointAddress"/>
        /// and equals the current end point address, false otherwise.
        /// </returns>
        public override bool Equals(object? obj)
        {
            return obj is RouteEndPointAddress endPointAddress && Equals(endPointAddress);
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>The generated hash code.</returns>
        public override int GetHashCode()
        {
            return Utf8EncodedValue.Span.SequenceHashCode();
        }

        /// <summary>
        /// Returns a stringified version of the end point address.
        /// </summary>
        /// <returns>A string representing the current end point address.</returns>
        public override string? ToString()
        {
            if (Utf8EncodedValue.IsEmpty)
                return null;

            return Encoding.UTF8.GetString(Utf8EncodedValue.Span);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two end point addresses are equal.
        /// </summary>
        /// <param name="left">The first end point address.</param>
        /// <param name="right">The second end point address.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(RouteEndPointAddress left, RouteEndPointAddress right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two end point addresses are unequal.
        /// </summary>
        /// <param name="left">The first end point address.</param>
        /// <param name="right">The second end point address.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(RouteEndPointAddress left, RouteEndPointAddress right)
        {
            return !left.Equals(right);
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("address-string", ToString());
        }
    }
}
