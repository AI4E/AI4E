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
using AI4E.Utils;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents the global entity query-result scope.
    /// </summary>
    public sealed class EntityQueryResultGlobalScope : IEntityQueryResultScope
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="EntityQueryResultGlobalScope"/> type.
        /// </summary>
        public static EntityQueryResultGlobalScope Instance { get; } = new EntityQueryResultGlobalScope();

        private EntityQueryResultGlobalScope() { }

        /// <inheritdoc/>
        public object ScopeEntity(object originalEntity)
        {
            if (originalEntity is null)
                throw new ArgumentNullException(nameof(originalEntity));

            return ObjectExtension.DeepClone(originalEntity)!;
        }
    }
}