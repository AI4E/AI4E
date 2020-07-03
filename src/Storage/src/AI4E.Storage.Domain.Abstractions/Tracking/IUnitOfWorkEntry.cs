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

namespace AI4E.Storage.Domain.Tracking
{
    /// <summary>
    /// Represents an entry tracked by a <see cref="IUnitOfWork{TLoadResult}"/>.
    /// </summary>
    /// <typeparam name="TLoadResult">The type of track-able load-result.</typeparam>
    public interface IUnitOfWorkEntry<out TLoadResult>
         where TLoadResult : class, IEntityLoadResult
    {
        /// <summary>
        /// Gets the tracked entity load result that is responsible for tracking the load-result.
        /// </summary>
        ITrackedEntityLoadResult<TLoadResult> EntityLoadResult { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the entry is modified.
        /// </summary>
        bool IsModified { get; }

        /// <summary>
        /// Gets the collection of recorded domain events.
        /// </summary>
        DomainEventCollection RecordedDomainEvents { get; } // TODO: Rename to DomainEvents?

        /// <summary>
        /// Creates a <see cref="IUnitOfWorkEntry{TLoadResult}"/> describing a delete operation on the current tracked entity.
        /// </summary>
        /// <param name="domainEvents">The collection of domain-events that were raised on the entity.</param>
        /// <returns>
        /// A unit-of work entry reflecting the delete operation.
        /// </returns>
        IUnitOfWorkEntry<TLoadResult> Delete(DomainEventCollection domainEvents);

        /// <summary>
        /// Creates a <see cref="IUnitOfWorkEntry{TLoadResult}"/> describing a create or update operation on the current tracked 
        /// entity.
        /// </summary>
        /// <param name="entity">The state of the updated tracked entity.</param>
        /// <param name="domainEvents">The collection of domain-events that were raised on the entity.</param>
        /// <returns>
        /// A unit-of work entry reflecting the create or update operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entity"/> is <c>null</c>.</exception>
        IUnitOfWorkEntry<TLoadResult> CreateOrUpdate(object entity, DomainEventCollection domainEvents);

        // TODO: Reset operation
    }
}
