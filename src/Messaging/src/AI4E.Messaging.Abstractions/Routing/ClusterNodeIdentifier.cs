/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace AI4E.Messaging.Routing
{
    /// <summary>
    /// Represents a unique identifier of a route end-point in a cluster of route end-points.
    /// </summary>
    [Serializable]
    public readonly struct ClusterNodeIdentifier
        : IEquatable<ClusterNodeIdentifier>, ISerializable
    {
        public static ClusterNodeIdentifier NoClusterNodeIdentifier { get; }

        public ClusterNodeIdentifier(ReadOnlySpan<byte> rawValue)
        {
            RawValue = rawValue.ToArray();
        }

        private ClusterNodeIdentifier(ReadOnlyMemory<byte> rawValue)
        {
            RawValue = rawValue; // TODO: Do we have to perform a copy for safety? See MessageFrame for reference.
        }

        private ClusterNodeIdentifier(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            if (serializationInfo is null)
                throw new ArgumentNullException(nameof(serializationInfo));

            if (serializationInfo.GetValue(nameof(RawValue), typeof(byte[])) is byte[] rawValue)
            {
                this = new ClusterNodeIdentifier((ReadOnlyMemory<byte>)rawValue);
            }
            else
            {
                this = default;
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            if (this != default)
            {
                byte[] array;

                if (MemoryMarshal.TryGetArray(RawValue, out var arraySegment)
                    && arraySegment.Array != null
                    && arraySegment.Offset == 0
                    && arraySegment.Count == arraySegment.Array.Length)
                {
                    array = arraySegment.Array;
                }
                else
                {
                    array = RawValue.ToArray();
                }

                info.AddValue(nameof(RawValue), array);
            }
        }

        public ReadOnlyMemory<byte> RawValue { get; }

        public static ClusterNodeIdentifier UnsafeCreateWithoutCopy(ReadOnlyMemory<byte> rawValue)
        {
            return new ClusterNodeIdentifier(rawValue);
        }

        public bool Equals(ClusterNodeIdentifier other)
        {
            return other.RawValue.Span.SequenceEqual(RawValue.Span);
        }

        public override bool Equals(object? obj)
        {
            return obj is ClusterNodeIdentifier clusterIdentifier && Equals(clusterIdentifier);
        }

        public override int GetHashCode()
        {
            return RawValue.Span.SequenceHashCode();
        }

        public static bool operator ==(ClusterNodeIdentifier left, ClusterNodeIdentifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClusterNodeIdentifier left, ClusterNodeIdentifier right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            if (this == NoClusterNodeIdentifier)
                return nameof(NoClusterNodeIdentifier);

            return Convert.ToBase64String(RawValue.Span, Base64FormattingOptions.None);
        }
    }
}
