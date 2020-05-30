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
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a unit of work that tracks entities and implements an identity map.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Gets a collection of all tracked entities.
        /// </summary>
        /// <returns></returns>
        IReadOnlyList<ITrackedEntity> TrackedEntities { get; }

        /// <summary>
        /// Gets a collection of all tracked entities thats state indicate modification.
        /// </summary>
        IReadOnlyList<ITrackedEntity> ModifiedEntities { get; }

        /// <summary>
        /// Tries to retrieve the tracked entity with the specified identifier.
        /// </summary>
        /// <param name="entityIdentifier">The entity identifier.</param>
        /// <param name="trackedEntity">Contains the tracked entity if present, <c>null</c> otherwise.</param>
        /// <returns>
        /// True if a tracked entity with the identity <paramref name="entityIdentifier"/> is present, false otherwise.
        /// </returns>
        bool TryGetTrackedEntity(
            EntityIdentifier entityIdentifier,
            [NotNullWhen(true)] out ITrackedEntity? trackedEntity);

        /// <summary>
        /// Updates the unit of work with the specified entity load-result if necessary 
        /// and returns the corresponding tracked entity.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <returns>The corresponding tracked entity to <paramref name="entityLoadResult"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// The unit of work is updated only if there is no tracked entity with the same identity present already. 
        /// The returned tracked entity is the tracked entity that was already present or that was created if an update
        /// was needed. If a tracked entity with the same identifier is already present, 
        /// <paramref name="entityLoadResult"/> is discarded.
        /// </remarks>
        ITrackedEntity GetOrUpdate(ICacheableEntityLoadResult entityLoadResult);

        /// <summary>
        /// Resets the unit of work to its initial state.
        /// </summary>
        void Reset();

        /// <summary>
        /// Asynchronously commits all tracked entity thats track state indicate modification 
        /// and resets the unit of work to the specified underlying storage engine.
        /// </summary>
        /// <param name="storageEngine">The underlying storage engine.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IEntityLoadResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the commit result.
        /// </returns>
        /// <remarks>
        /// Regardless of the commit result, the unit of work is reset to its initial state.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="storageEngine"/> is <c>null</c>.
        /// </exception>
        ValueTask<EntityCommitResult> CommitAsync(
            IEntityStorageEngine storageEngine,
            CancellationToken cancellation = default);
    }
}