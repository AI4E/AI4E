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
    /// A base class for domain entity implementations.
    /// </summary>
    public class DomainEntity : IDomainEntity
    {
        private readonly List<DomainEvent> _domainEvents = new List<DomainEvent>();

        /// <summary>
        /// Creates a new instance of the <see cref="DomainEntity"/> type in a derived class.
        /// </summary>
        protected internal DomainEntity() { }

        /// <inheritdoc/>
        public string? Id { get; set; }

        /// <inheritdoc/>
        public ConcurrencyToken ConcurrencyToken { get; set; }

        /// <inheritdoc/>
        public long Revision { get; set; }

        /// <inheritdoc/>
        public void CommitEvents()
        {
            _domainEvents.Clear();
        }

        /// <inheritdoc/>
        public IEnumerable<DomainEvent> GetUncommittedEvents()
        {
            return _domainEvents;
        }

        /// <inheritdoc/>
        public void AddEvent(DomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }
    }
}
