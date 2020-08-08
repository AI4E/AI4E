/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
    /// Represents a manager responsible for entity meta-data.
    /// </summary>
    public interface IEntityMetadataManager
    {
        /// <summary>
        /// Gets the id of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <returns>The id of the entity described by <paramref name="entityDescriptor"/>.</returns>
        string? GetId(EntityDescriptor entityDescriptor);

        /// <summary>
        /// Sets the id of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <param name="id">The desired entity id.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="id"/> is <c>null</c>.</exception>
        void SetId(EntityDescriptor entityDescriptor, string id); // TODO: Rename id to entityId

        /// <summary>
        /// Gets the concurrency-token of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <returns>The concurrency-token of the entity described by <paramref name="entityDescriptor"/>.</returns>
        ConcurrencyToken GetConcurrencyToken(EntityDescriptor entityDescriptor);

        /// <summary>
        /// Sets the concurrency-token of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <param name="concurrencyToken">The desired entity concurrency-token.</param>
        void SetConcurrencyToken(EntityDescriptor entityDescriptor, ConcurrencyToken concurrencyToken);

        /// <summary>
        /// Gets the revision of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <returns>The revision of the entity described by <paramref name="entityDescriptor"/>.</returns>
        long GetRevision(EntityDescriptor entityDescriptor);

        /// <summary>
        /// Sets the revision of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <param name="revision">The desired entity revision.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="revision"/> is negative.</exception>
        void SetRevision(EntityDescriptor entityDescriptor, long revision);

        /// <summary>
        /// Commits all domain-events of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        void CommitEvents(EntityDescriptor entityDescriptor);

        /// <summary>
        /// Gets the collection of all uncommitted domain-events of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <returns>A <see cref="DomainEventCollection"/> of all uncommitted domain-events.</returns>
        DomainEventCollection GetUncommittedEvents(EntityDescriptor entityDescriptor);

        /// <summary>
        /// Raises the specified domain-event on the specified entity.
        /// </summary>
        ///  <param name="entityDescriptor">A descriptor that describes the entity.</param>
        /// <param name="domainEvent">The domain-event.</param>
        void AddEvent(EntityDescriptor entityDescriptor, DomainEvent domainEvent);
    }
}
