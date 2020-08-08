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
using System.Collections.Generic;
using AI4E.Utils;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityQueryResultScope"/>
    public sealed class EntityQueryResultScope : IEntityQueryResultScope
    {
        private readonly Dictionary<object, object> _scopedEntities;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityQueryResultScope"/> type.
        /// </summary>
        public EntityQueryResultScope()
        {
            _scopedEntities = new Dictionary<object, object>();
        }

        /// <inheritdoc/>
        public object ScopeEntity(object originalEntity)
        {
            if (originalEntity is null)
                throw new ArgumentNullException(nameof(originalEntity));

            if(!_scopedEntities.TryGetValue(originalEntity, out var scopedEntity))
            {
                // TODO: Null forgiving operator should not be needed here??
                scopedEntity = ObjectExtension.DeepClone(originalEntity)!; 
                _scopedEntities.Add(originalEntity, scopedEntity);

                // We add the scoped entity as key too, so that when we request to scope the already scoped entity, 
                // we do not scope (= copy) it another time.
                _scopedEntities.Add(scopedEntity, scopedEntity);
            }

            return scopedEntity;
        }
    }
}
