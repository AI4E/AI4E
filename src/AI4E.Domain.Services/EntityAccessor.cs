/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Diagnostics;
using System.Linq;
using AI4E.Storage;

namespace AI4E.Domain.Services
{
    public sealed class EntityAccessor : IEntityAccessor<Guid, DomainEvent, AggregateRoot>
    {
        public EntityAccessor() { }

        public Guid GetId(AggregateRoot entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entity.Id;
        }

        public Guid GetConcurrencyToken(AggregateRoot entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entity.ConcurrencyToken;
        }

        public void SetConcurrencyToken(AggregateRoot entity, Guid concurrencyToken)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entity.ConcurrencyToken = concurrencyToken;
        }

        public long GetRevision(AggregateRoot entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return entity.Revision;
        }

        public void SetRevision(AggregateRoot entity, long revision)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            Debug.Assert(revision > 0);

            entity.Revision = revision;
        }

        public IEnumerable<DomainEvent> GetUncommittedEvents(AggregateRoot entity)
        {
            if (entity == null)
                return null;

            return entity.UncommittedEvents ?? Enumerable.Empty<DomainEvent>();
        }

        public void CommitEvents(AggregateRoot entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entity.CommitEvents();
        }
    }
}
