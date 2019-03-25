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

namespace AI4E.Coordination.Utils
{
    public readonly struct StringAddress : IEquatable<StringAddress>
    {
        public StringAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("TODO", nameof(address));

            Address = address;
        }

        public string Address { get; }

        public bool Equals(StringAddress other)
        {
            return Address == other.Address;
        }

        public override bool Equals(object obj)
        {
            return obj is StringAddress address && Equals(address);
        }

        public override int GetHashCode()
        {
            return Address?.GetHashCode() ?? 0;
        }

        public static bool operator ==(StringAddress left, StringAddress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StringAddress left, StringAddress right)
        {
            return !left.Equals(right);
        }
    }
}
