﻿/* License
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
using System.Collections.Generic;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a descriptor for an entity.
    /// </summary>
    public readonly struct EntityDescriptor : IEquatable<EntityDescriptor>
    {
        private static readonly object _objectInstance = new object();

        private readonly Type? _entityType;
        private readonly object? _entity;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityDescriptor"/> type from the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="entity"/> is a delegate or a value-type.
        /// </exception>
        public EntityDescriptor(object entity)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            EntityValidationHelper.Validate(entity);

            _entityType = entity.GetType();
            _entity = entity;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityDescriptor"/> type from the specified type of entity 
        /// and entity.
        /// </summary>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entity">The entity.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="entityType"/> or <paramref name="entity"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if 
        /// <paramref name="entity"/> is not of type <paramref name="entityType"/> or assignable to it 
        /// -- OR -- 
        /// <paramref name="entityType"/> specifies a delegate type, a value-type,
        /// an interface type or an open generic type definition.
        /// -- OR --
        /// <paramref name="entity"/> is a delegate or a value-type.
        /// </exception>
        public EntityDescriptor(Type entityType, object entity)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            EntityValidationHelper.Validate(entityType, entity);

            _entityType = entityType;
            _entity = entity;
        }

        /// <summary>
        /// Gets the type of entity.
        /// </summary>
        public Type EntityType => _entityType ?? typeof(object);

        /// <summary>
        /// Gets the entity.
        /// </summary>
        public object Entity => _entity ?? _objectInstance;

        /// <summary>
        /// Deconstructs the current entity-descriptor.
        /// </summary>
        /// <param name="entityType">Contains the type of entity.</param>
        /// <param name="entity">Contains the entity.</param>
        public void Deconstruct(out Type entityType, out object entity)
        {
            entityType = EntityType;
            entity = Entity;
        }

        /// <inheritdoc cref="IEquatable{EntityDescriptor}.Equals(EntityDescriptor)"/>
        public bool Equals(in EntityDescriptor other)
        {
            return EntityType == other.EntityType && EqualityComparer<object>.Default.Equals(Entity, other.Entity);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is EntityDescriptor entityDescriptor && Equals(in entityDescriptor);
        }

        bool IEquatable<EntityDescriptor>.Equals(EntityDescriptor other)
        {
            return Equals(in other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(EntityType, Entity);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two entity descriptors are equal.
        /// </summary>
        /// <param name="left">The first <see cref="EntityDescriptor"/>.</param>
        /// <param name="right">The second <see cref="EntityDescriptor"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in EntityDescriptor left, in EntityDescriptor right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two entity descriptors are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="EntityDescriptor"/>.</param>
        /// <param name="right">The second <see cref="EntityDescriptor"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in EntityDescriptor left, in EntityDescriptor right)
        {
            return !left.Equals(in right);
        }
    }
}
