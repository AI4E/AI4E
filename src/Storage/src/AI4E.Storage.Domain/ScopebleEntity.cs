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
using System.Runtime.CompilerServices;
using AI4E.Utils;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents an entity scoped to a single <see cref="IEntityStorage"/>.
    /// </summary>
    public sealed class ScopebleEntity
    {
        private readonly object _originalEntity;

        // TODO: Measure the perf of this. 
        //       Is it that good, that we create a conditional weak table for each potential entity loadable?
        //       It would be better if we had a static global table that is index by both,
        //       the entity storage and the entity.
        private readonly ConditionalWeakTable<IEntityStorage, object> _scopedEntities;
        private readonly ConditionalWeakTable<IEntityStorage, object>.CreateValueCallback _createScopedEntity;

        /// <summary>
        /// Creates a new instance of the <see cref="ScopebleEntity"/> type with the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is <c>null</c>.</exception>
        public ScopebleEntity(object entity)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            _originalEntity = entity;

            _scopedEntities = new ConditionalWeakTable<IEntityStorage, object>();
            _createScopedEntity = CreateScopedEntity;
        }

        /// <summary>
        /// Returns the entity scoped to the specified entity storage.
        /// </summary>
        /// <param name="entityStorage">The entity storage that defines the entity scope.</param>
        /// <returns>
        /// The entity scoped to <paramref name="entityStorage"/>. If <paramref name="entityStorage"/> is <c>null</c>, 
        /// the unscoped entity is returned.
        /// </returns>
        public object GetEntity(IEntityStorage? entityStorage)
        {
            if (entityStorage is null)
            {
                // TODO: Do we return the original entity or a copy?
                return _originalEntity;
            }

            return _scopedEntities.GetValue(entityStorage, _createScopedEntity);
        }

        private object GetEntityCopy()
        {
            return _originalEntity.DeepClone()!;
        }

        private object CreateScopedEntity(IEntityStorage entityStorage)
        {
            return GetEntityCopy();
        }
    }
}