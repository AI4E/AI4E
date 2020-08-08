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

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Describes an entity's dependency.
    /// </summary>
    public readonly struct EntityDependency : IEquatable<EntityDependency>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EntityDependency"/> type.
        /// </summary>
        /// <param name="entityType">The type of the projection source.</param>
        /// <param name="entityId">The id of the projection source.</param>
        /// <param name="projectionRevision">The revision of the source that the projection is based on.</param>
        ///  <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entityType"/> or <paramref name="entityId"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="entityId"/> is empty or consists of whitespace only.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="projectionRevision"/> is negative.</exception>
        public EntityDependency(Type entityType, string entityId, long projectionRevision)
            : this(new EntityIdentifier(entityType, entityId), projectionRevision)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityDependency"/> type.
        /// </summary>
        /// <param name="dependency">A <see cref="EntityIdentifier"/> representing the projection source.</param>
        /// <param name="projectionRevision">The revision of the source that the projection is based on.</param>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="dependency"/> is <c>default</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="projectionRevision"/> is negative.</exception>
        public EntityDependency(in EntityIdentifier dependency, long projectionRevision)
        {
            if (dependency == default)
                throw new ArgumentDefaultException(nameof(dependency));

            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));

            Dependency = dependency;
            ProjectionRevision = projectionRevision;
        }

        /// <summary>
        /// Gets a descriptor for the dependency's projection source.
        /// </summary>
        public EntityIdentifier Dependency { get; }

        /// <summary>
        /// Gets the revision of the source that the projection is based on.
        /// </summary>
        public long ProjectionRevision { get; }

        /// <inheritdoc/>
        public bool Equals(EntityDependency other)
        {
            return (Dependency, ProjectionRevision) == (other.Dependency, other.ProjectionRevision);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is EntityDependency projectionSourceDependency
                && Equals(projectionSourceDependency);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Dependency, ProjectionRevision);
        }

        /// <summary>
        /// Checks two instances of <see cref="EntityDependency"/> to be equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in EntityDependency left, in EntityDependency right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks two instances of <see cref="EntityDependency"/> to be non-equal.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in EntityDependency left, in EntityDependency right)
        {
            return !left.Equals(right);
        }
    }
}
