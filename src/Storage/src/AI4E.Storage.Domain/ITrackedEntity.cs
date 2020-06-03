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
    /// Represents an entity tracked by an <see cref="IUnitOfWork"/>.
    /// </summary>
    /// <remarks>
    /// Implementations must be thread-safe (or immutable).
    /// </remarks>
    internal interface ITrackedEntity
    {
        /// <summary>
        /// Gets the track-state of the tracked entity.
        /// </summary>
        EntityTrackState TrackState { get; }

        /// <summary>
        /// Gets the entity load-result that the tracked entity was originally created from.
        /// </summary>
        ICacheableEntityLoadResult OriginalEntityLoadResult { get; }

        /// <summary>
        /// Gets the updated revision or <c>null</c> if the revision was not updated.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be non-null if <see cref="TrackState"/> indicates modification.
        /// </remarks>
        long? UpdatedRevision { get; }

        /// <summary>
        /// Gets the updated concurrency-token or <c>null</c> if the concurrency-token was not updated.
        /// </summary>
        /// <remarks>
        /// This is guaranteed to be non-null if <see cref="TrackState"/> indicates modification.
        /// </remarks>
        ConcurrencyToken? UpdatedConcurrencyToken { get; }

        /// <summary>
        /// Gets the collection of domain-events that were raised on the entity.
        /// </summary>
        DomainEventCollection DomainEvents { get; }

        /// <summary>
        /// Gets an entity load-result that represents the tracked entity in the way that it behaves like the tracked 
        /// entity was already committed and the entity loaded freshly from the underlying store.
        /// </summary>
        /// <remarks>
        /// This reflects the current state of the tracked entity with the modifications and falls back to the original 
        /// load-result if there are no changes.
        /// </remarks>
        ICacheableEntityLoadResult EntityLoadResult { get; }

        /// <summary>
        /// Gets the current state of the tracked entity.
        /// </summary>
        /// <remarks>
        /// This reflects the current state of the tracked entity with the modifications and falls back to the original 
        /// load-result if there are no changes.
        /// </remarks>
        object? Entity { get; }

        /// <summary>
        /// Gets the concurrency-token of the tracked entity.
        /// </summary>
        /// <remarks>
        /// This reflects the current state of the tracked entity with the modifications and falls back to the original 
        /// load-result if there are no changes.
        /// This is guaranteed never to return a default concurrency-token.
        /// </remarks>
        ConcurrencyToken ConcurrencyToken { get; }

        /// <summary>
        /// Gets the revision of the tracked entity.
        /// </summary>
        /// <remarks>
        /// This reflects the current state of the tracked entity with the modifications and falls back to the original 
        /// load-result if there are no changes.
        /// </remarks>
        long Revision { get; }

        /// <summary>
        /// Creates an <see cref="ITrackedEntity"/> describing a delete operation on the current tracked entity.
        /// </summary>
        /// <param name="domainEvents">The collection of domain-events that were raised on the entity.</param>
        /// <returns>
        /// A tracked-entity reflecting the delete operation, 
        /// or <c>null</c> if the current tracked entity reflects a create operation.
        /// </returns>
        ITrackedEntity? Delete(DomainEventCollection domainEvents);

        /// <summary>
        /// Creates an <see cref="ITrackedEntity"/> describing a create or update operation on the current tracked 
        /// entity.
        /// </summary>
        /// <param name="entity">The state of the updated tracked entity.</param>
        /// <param name="domainEvents">The collection of domain-events that were raised on the entity.</param>
        /// <returns>
        /// A tracked-entity reflecting the create or update operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is <c>null</c>.</exception>
        ITrackedEntity CreateOrUpdate(object entity, DomainEventCollection domainEvents);
    }
}
