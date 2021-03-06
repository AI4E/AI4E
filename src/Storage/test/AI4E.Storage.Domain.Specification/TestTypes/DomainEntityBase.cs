﻿/* License
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
using System.Collections.Immutable;

namespace AI4E.Storage.Domain.Specification.TestTypes
{
    public sealed class DomainEntity<TValue> : DomainEntityBase
    {
        public TValue Value { get; set; }
    }

    public abstract class DomainEntityBase : IDomainEntity
    {
        private readonly HashSet<DomainEvent> _domainEvents = new HashSet<DomainEvent>();

        public string? Id { get; set; }
        public ConcurrencyToken ConcurrencyToken { get; set; }
        public long Revision { get; set; }

        public void CommitEvents()
        {
            _domainEvents.Clear();
        }

        public IEnumerable<DomainEvent> GetUncommittedEvents()
        {
            return _domainEvents.ToImmutableList();
        }

        public void AddEvent(DomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }
    }
}
