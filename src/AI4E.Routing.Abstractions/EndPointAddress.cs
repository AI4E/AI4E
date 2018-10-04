/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EndPointAddress.cs 
 * Types:           AI4E.Routing.EndPointAddress
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.10.2018 
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
using System.Collections.Concurrent;

namespace AI4E.Routing
{
    /// <summary>
    /// Represents the address of a logical end point.
    /// </summary>
    [Serializable]
    public sealed class EndPointAddress : IEquatable<EndPointAddress>
    {
        private static readonly ConcurrentDictionary<string, EndPointAddress> _instances = new ConcurrentDictionary<string, EndPointAddress>();

        // TODO: private -> Fix serialization
        public EndPointAddress(string logicalAddress)
        {
            LogicalAddress = logicalAddress;
        }

        /// <summary>
        /// Gets a stringified version of the end point address.
        /// </summary>
        public string LogicalAddress { get; }

        /// <summary>
        /// Creates a new end point address from the specified string.
        /// </summary>
        /// <param name="logicalAddress">The string that specifies the end point address.</param>
        /// <returns>The created end point address.</returns>
        /// <exception cref="ArgumentNullOrWhiteSpaceException">Thrown if <paramref name="logicalAddress"/> is either null, an empty string or a string consisting of whitespace only.</exception>
        public static EndPointAddress Create(string logicalAddress)
        {
            if (string.IsNullOrWhiteSpace(logicalAddress))
            {
                throw new ArgumentNullOrWhiteSpaceException(nameof(logicalAddress));
            }

            return _instances.GetOrAdd(logicalAddress, _ => new EndPointAddress(logicalAddress));
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specifies end point address equals the current instance.
        /// </summary>
        /// <param name="other">The end point address to compare to.</param>
        /// <returns>True if <paramref name="other"/> equals the current end point address, false otherwise.</returns>
        public bool Equals(EndPointAddress other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(other, this))
                return true;

            return other.LogicalAddress == LogicalAddress;
        }

        /// <summary>
        /// Return a boolean value indicating whether the specifies object equals the current end point address.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if <paramref name="obj"/> is of type <see cref="EndPointAddress"/> and equals the current end point address, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is EndPointAddress endPointAddress && Equals(endPointAddress);
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>The generated hash code.</returns>
        public override int GetHashCode()
        {
            return LogicalAddress.GetHashCode();
        }

        /// <summary>
        /// Returns a stringified version of the end point address.
        /// </summary>
        /// <returns>A string representing the current end point address.</returns>
        public override string ToString()
        {
            return LogicalAddress;
        }

        /// <summary>
        /// Returns a boolean value indicating whether two end point addresses are equal.
        /// </summary>
        /// <param name="left">The first end point address.</param>
        /// <param name="right">The second end point address.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(EndPointAddress left, EndPointAddress right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two end point addresses are inequal.
        /// </summary>
        /// <param name="left">The first end point address.</param>
        /// <param name="right">The second end point address.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(EndPointAddress left, EndPointAddress right)
        {
            if (left is null)
                return !(right is null);

            return !left.Equals(right);
        }
    }
}
