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

using System.Collections.Generic;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a domain entity.
    /// </summary>
    public interface IDomainEntity
    {
        /// <summary>
        /// Gets or sets the entity id.
        /// </summary>
        string? Id { get; set; }

        /// <summary>
        /// Gets or sets the entity concurrency-token.
        /// </summary>
        ConcurrencyToken ConcurrencyToken { get; set; }

        /// <summary>
        /// Gets or sets the entity revision.
        /// </summary>
        long Revision { get; set; }

        /// <summary>
        /// Commits all domain-events-
        /// </summary>
        void CommitEvents();

        /// <summary>
        /// Returns a collection of raised domain-events.
        /// </summary>
        /// <returns>A collection of raised domain-events.</returns>
        IEnumerable<DomainEvent> GetUncommittedEvents(); // TODO: Use DomainEventCollection

        /// <summary>
        /// Raises the specified domain-event.
        /// </summary>
        /// <param name="domainEvent">The domain-event to raise.</param>
        void AddEvent(DomainEvent domainEvent); // TODO: Rename to RaiseEvent
    }
}
