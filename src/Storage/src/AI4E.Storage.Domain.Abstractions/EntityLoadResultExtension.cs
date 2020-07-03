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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Contains exception for the <see cref="IEntityLoadResult"/> type.
    /// </summary>
    public static class EntityLoadResultExtension
    {
        /// <summary>
        /// Returns the successfully loaded entity or <c>null</c> if the specified load-result indicates failure.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <param name="throwOnFailure">
        /// A boolean value indicating whether an exception shall be thrown if the entity could not be loaded.
        /// </param>
        /// <returns>
        /// The entity extracted if the current entity load-result indicates success, <c>null</c> otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="EntityLoadException">
        /// Thrown if <paramref name="throwOnFailure"/> is true and the entity could not be loaded for a reason 
        /// other then non-existence.
        /// </exception>
        public static object? GetEntity(this IEntityLoadResult entityLoadResult, bool throwOnFailure = true)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            if (entityLoadResult.IsVerificationFailed(out var entityVerificationResult))
            {
                if (throwOnFailure)
                {
                    throw new EntityVerificationException(entityVerificationResult);
                }

                return null;
            }

            if (entityLoadResult.IsFound(out var foundEntityQueryResult))
            {
                return foundEntityQueryResult.Entity;
            }

            return null;
        }

        /// <summary>
        /// Checks whether the current entity load-result represents a found entity query-result.
        /// </summary>
        /// <param name="entityLoadResult"> The entity load-result. </param>
        /// <returns>
        /// True if the current entity load-result represents a found entity query-result, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown of <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        public static bool IsFound(this IEntityLoadResult entityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            return entityLoadResult.IsFound(out _);
        }

        /// <summary>
        /// Returns a entity-load result representing the current entity load-result scoped to the specified 
        /// query-result scope or the original entity load-result if it is not scopeable.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <param name="scope">The <see cref="IEntityQueryResultScope"/> that defines the scope.</param>
        /// <returns>
        /// A <see cref="IEntityLoadResult"/> that is scoped to <paramref name="scope"/> 
        /// if <paramref name="entityLoadResult"/> is scopeable, <paramref name="entityLoadResult"/> otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown of either <paramref name="entityLoadResult"/> or <paramref name="scope"/> is <c>null</c>.
        /// </exception>
        public static IEntityLoadResult AsScopedTo(
            this IEntityLoadResult entityLoadResult,
            IEntityQueryResultScope scope)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (entityLoadResult.IsScopeable<IEntityQueryResult>(out var scopeableEntityQueryResult))
            {
                var scopedEntityQueryResult = scopeableEntityQueryResult.AsScopedTo(scope);
                entityLoadResult = scopedEntityQueryResult.ToQueryResult();
            }

            return entityLoadResult;
        }

        /// <summary>
        /// Checks whether the current entity load-result is scopeable.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <param name="scopeableEntityQueryResult">
        /// Contains the <see cref="IScopeableEntityQueryResult{IEntityQueryResult}"/> if the current entity load-result 
        /// is scopeable.
        /// </param>
        /// <returns>
        /// True if the current entity load-result is scopeable, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        public static bool IsScopeable(
            this IEntityLoadResult entityLoadResult,
            [NotNullWhen(true)] out IScopeableEntityQueryResult<IEntityQueryResult>? scopeableEntityQueryResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            return entityLoadResult.IsScopeable(out scopeableEntityQueryResult);
        }

        /// <summary>
        /// Checks whether the current entity load-result is a track-able entity load-result.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <param name="trackableEntityLoadResult">
        /// Contains the <see cref="ITrackableEntityLoadResult{IEntityLoadResult}"/> if the current entity load-result 
        /// is track-able.
        /// </param>
        /// <returns>
        /// True if the current entity load-result is track-able, 
        /// false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        public static bool IsTrackable(
            this IEntityLoadResult entityLoadResult,
            [NotNullWhen(true)] out ITrackableEntityLoadResult<IEntityLoadResult>? trackableEntityLoadResult)
        {
            if (entityLoadResult is null)
                throw new ArgumentNullException(nameof(entityLoadResult));

            return entityLoadResult.IsTrackable(out trackableEntityLoadResult);
        }
    }
}
