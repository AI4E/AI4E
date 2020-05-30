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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents an identifier for an entity.
    /// </summary>
    public readonly struct EntityIdentifier : IEquatable<EntityIdentifier>
    {
        private readonly Type? _entityType;
        private readonly string? _entityId;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityIdentifier"/> type from the specified type of entity 
        /// and entity id.
        /// </summary>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entityId">The entity id.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="entityType"/> or <paramref name="entityId"/> is <c>null</c>.
        /// </exception>
        public EntityIdentifier(Type entityType, string entityId)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityId is null)
                throw new ArgumentNullException(nameof(entityId));

            // TODO: Validate entity type.

            _entityType = entityType;
            _entityId = entityId;
        }

        /// <summary>
        /// Get the type of entity.
        /// </summary>
        public Type EntityType => _entityType ?? typeof(object);

        /// <summary>
        /// Gets the entity id.
        /// </summary>
        public string EntityId => _entityId ?? string.Empty;

        /// <inheritdoc cref="IEquatable{EntityIdentifier}.Equals(EntityIdentifier)"/>
        public bool Equals(in EntityIdentifier other)
        {
            return (EntityType, EntityId) == (other.EntityType, other.EntityId);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is EntityIdentifier entityIdentifier && Equals(in entityIdentifier);
        }

        bool IEquatable<EntityIdentifier>.Equals(EntityIdentifier other)
        {
            return Equals(in other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(EntityType, EntityId);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two entity identifiers are equal.
        /// </summary>
        /// <param name="left">The first <see cref="EntityIdentifier"/>.</param>
        /// <param name="right">The second <see cref="EntityIdentifier"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in EntityIdentifier left, in EntityIdentifier right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two entity identifiers are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="EntityIdentifier"/>.</param>
        /// <param name="right">The second <see cref="EntityIdentifier"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in EntityIdentifier left, in EntityIdentifier right)
        {
            return !left.Equals(in right);
        }
    }
}
