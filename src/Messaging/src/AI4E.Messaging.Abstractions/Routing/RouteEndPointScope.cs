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
using System.IO;

namespace AI4E.Messaging.Routing
{
    public readonly struct RouteEndPointScope : IEquatable<RouteEndPointScope>
    {
        public static RouteEndPointScope NoScope { get; }

        public RouteEndPointScope(
            RouteEndPointAddress endPointAddress,
            ClusterNodeIdentifier clusterNodeIdentifier,
            long? scopeIdentifier = null)
        {
            EndPointAddress = endPointAddress;
            ClusterNodeIdentifier = clusterNodeIdentifier;
            ScopeIdentifier = scopeIdentifier;
        }

        public RouteEndPointScope(RouteEndPointAddress endPointAddress)
        {
            EndPointAddress = endPointAddress;
            ClusterNodeIdentifier = ClusterNodeIdentifier.NoClusterNodeIdentifier;
            ScopeIdentifier = 0;
        }

        /// <summary>
        /// Gets the end-point address that identifies the end-point.
        /// </summary>
        public RouteEndPointAddress EndPointAddress { get; }

        /// <summary>
        /// Gets the end-point cluster node identifier that identifies the node in a cluster of end-point nodes with 
        /// address <see cref="RouteEndPointAddress"/> or <see cref="ClusterNodeIdentifier.NoClusterNodeIdentifier"/> 
        /// if the node identifier is not specified.
        /// </summary>
        public ClusterNodeIdentifier ClusterNodeIdentifier { get; }

        /// <summary>
        /// Gets the scope identifier that identifies the scope of the end-point cluster node.
        /// </summary>
        public long? ScopeIdentifier { get; }

        public bool CanBeRoutedTo(in RouteEndPointScope target)
        {
            if (target == NoScope)
                return true;

            if (EndPointAddress != target.EndPointAddress)
                return false;

            if (target.ClusterNodeIdentifier == ClusterNodeIdentifier.NoClusterNodeIdentifier)
                return true;

            if (ClusterNodeIdentifier != target.ClusterNodeIdentifier)
                return false;

            if (target.ScopeIdentifier == null)
                return true;

            return ScopeIdentifier == target.ScopeIdentifier;
        }

        public bool Equals(RouteEndPointScope other)
        {
            return Equals(in other);
        }

        public bool Equals(in RouteEndPointScope other)
        {
            return EndPointAddress == other.EndPointAddress &&
                   ClusterNodeIdentifier == other.ClusterNodeIdentifier &&
                   ScopeIdentifier == other.ScopeIdentifier;
        }

        public override bool Equals(object? obj)
        {
            return obj is RouteEndPointScope routeEndPointScope && Equals(in routeEndPointScope);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EndPointAddress, ClusterNodeIdentifier, ScopeIdentifier);
        }

        public override string ToString()
        {
            if (this == NoScope)
            {
                return nameof(NoScope);
            }

            if (ClusterNodeIdentifier == ClusterNodeIdentifier.NoClusterNodeIdentifier)
            {
                return EndPointAddress.ToString();
            }

            if (ScopeIdentifier is null)
            {
                return $"{EndPointAddress} Cluster node: {ClusterNodeIdentifier}";
            }

            return $"{EndPointAddress} Cluster node: {ClusterNodeIdentifier} Scope: {ScopeIdentifier}";
        }

        public static bool operator ==(in RouteEndPointScope left, in RouteEndPointScope right)
        {
            return left.Equals(in right);
        }

        public static bool operator !=(in RouteEndPointScope left, in RouteEndPointScope right)
        {
            return !left.Equals(in right);
        }

        public static void Write(BinaryWriter writer, in RouteEndPointScope routeEndPointScope)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            writer.WriteBytes(routeEndPointScope.EndPointAddress.Utf8EncodedValue.Span);
            writer.WriteBytes(routeEndPointScope.ClusterNodeIdentifier.RawValue.Span);
            writer.Write(routeEndPointScope.ScopeIdentifier.HasValue);

            if (routeEndPointScope.ScopeIdentifier.HasValue)
                writer.Write(routeEndPointScope.ScopeIdentifier.Value);
        }

        public static void Read(BinaryReader reader, out RouteEndPointScope routeEndPointScope)
        {
            if (reader is null)
                throw new ArgumentNullException(nameof(reader));

            var addressRawValue = reader.ReadBytes();
            var clusterIdentifierRawValue = reader.ReadBytes();
            var hasScopeIdentifier = reader.ReadBoolean();
            var scopeIdentifier = hasScopeIdentifier ? (long?)reader.ReadInt64() : null;

            var endPointAddress = new RouteEndPointAddress(addressRawValue);
            var endPointClusterIdentifier = ClusterNodeIdentifier.UnsafeCreateWithoutCopy(
                clusterIdentifierRawValue);

            routeEndPointScope = new RouteEndPointScope(endPointAddress, endPointClusterIdentifier, scopeIdentifier);
        }
    }
}
