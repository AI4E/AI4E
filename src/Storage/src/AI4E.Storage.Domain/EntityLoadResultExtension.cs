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
    /// Contains extensions for the <see cref="IEntityLoadResult"/> type.
    /// </summary>
    public static class EntityLoadResultExtension
    {
        /// <summary>
        /// Converts the specified entity load-result to a success entity load-result.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result to convert.</param>
        /// <returns>
        /// An <see cref="ISuccessEntityLoadResult"/> created from <paramref name="entityLoadResult"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="entityLoadResult"/> does not indicate success.
        /// </exception>
        public static ISuccessEntityLoadResult AsSuccessLoadResult(
            this IEntityLoadResult entityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            if (entityLoadResult is ISuccessEntityLoadResult successEntityLoadResult)
                return successEntityLoadResult;

            if (!entityLoadResult.IsSuccess(out var entity))
                throw new ArgumentException(Resources.EntityLoadResultMustIndicateSuccess);

            return new SuccessEntityLoadResult(
                entityLoadResult.EntityIdentifier,
                entity,
                entityLoadResult.ConcurrencyToken,
                entityLoadResult.Revision,
                ((ICacheableEntityLoadResult)entityLoadResult).LoadedFromCache);
        }
    }
}