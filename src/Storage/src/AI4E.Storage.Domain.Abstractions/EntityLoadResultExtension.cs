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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Contains extension methods for the <see cref="IEntityLoadResult"/> type.
    /// </summary>
    public static class EntityLoadResultExtension
    {
        /// <summary>
        /// Returns the <see cref="EntityDescriptor"/> for the current load-result.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <returns>The <see cref="EntityDescriptor"/> created from <paramref name="entityLoadResult"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityDescriptor GetEntityDescriptor(this IFoundEntityQueryResult entityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            return new EntityDescriptor(entityLoadResult.EntityIdentifier.EntityType, entityLoadResult.Entity);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified entity load-result indicates success.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <returns>True if <paramref name="entityLoadResult"/> indicates success, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSuccess(this IEntityLoadResult entityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            return entityLoadResult.GetEntity(throwOnFailure: false) != null;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified entity load-result indicates success 
        /// and extracts the entity.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <param name="entity">
        /// Contains the extracted entity if <paramref name="entityLoadResult"/> indicates success,
        /// <c>null</c> otherwise.
        /// </param>
        /// <returns>True if <paramref name="entityLoadResult"/> indicates success, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSuccess(this IEntityLoadResult entityLoadResult, [NotNullWhen(true)] out object? entity)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            entity = entityLoadResult.GetEntity(throwOnFailure: false);
            return entity != null;
        }
    }
}
