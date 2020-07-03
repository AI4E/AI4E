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
    /// Contains extension methods for the <see cref="IEntityLoadResult"/> type.
    /// </summary>
    public static class EntityQueryResultExtension
    {
        /// <summary>
        /// Returns a entity-query result representing the current entity query-result scoped to the specified 
        /// query-result scope or the original entity query-result if it is not scopeable.
        /// </summary>
        /// <param name="entityQueryResult">The entity query-result.</param>
        /// <param name="scope">The <see cref="IEntityQueryResultScope"/> that defines the scope.</param>
        /// <returns>
        /// A <see cref="IEntityQueryResult"/> that is scoped to <paramref name="scope"/> 
        /// if <paramref name="entityQueryResult"/> is scopeable, <paramref name="entityQueryResult"/> otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown of either <paramref name="entityQueryResult"/> or <paramref name="scope"/> is <c>null</c>.
        /// </exception>
        public static IEntityQueryResult AsScopedTo(
            this IEntityQueryResult entityQueryResult,
            IEntityQueryResultScope scope)
        {
            if (entityQueryResult is null)
                throw new ArgumentNullException(nameof(entityQueryResult));

            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (entityQueryResult.IsScopeable<IEntityQueryResult>(out var scopeableEntityQueryResult))
            {
                var scopedEntityQueryResult = scopeableEntityQueryResult.AsScopedTo(scope);
                entityQueryResult = scopedEntityQueryResult.ToQueryResult();
            }

            return entityQueryResult;
        }
    }
}
