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

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityMetadataManager"/>
    public sealed class EntityMetadataManager : IEntityMetadataManager
    {
        private readonly Dictionary<EntityDescriptor, DomainEntity> _domainEntites
            = new Dictionary<EntityDescriptor, DomainEntity>();

        /// <inheritdoc/>
        public string? GetId(EntityDescriptor entityDescriptor)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                return ((IDomainEntity)entityDescriptor.Entity).Id;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessId)
            {
                return accessor.GetId(entityDescriptor.Entity);
            }

            if (_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                return domainEntity.Id;
            }

            return null;
        }

        /// <inheritdoc/>
        public void SetId(EntityDescriptor entityDescriptor, string id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                ((IDomainEntity)entityDescriptor.Entity).Id = id;
                return;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessId)
            {
                accessor.SetId(entityDescriptor.Entity, id);
                return;
            }

            if (!_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                domainEntity = new DomainEntity();
                _domainEntites.Add(entityDescriptor, domainEntity);
            }

            domainEntity.Id = id;
        }

        /// <inheritdoc/>
        public ConcurrencyToken GetConcurrencyToken(EntityDescriptor entityDescriptor)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                return ((IDomainEntity)entityDescriptor.Entity).ConcurrencyToken;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessConcurrencyToken)
            {
                return accessor.GetConcurrencyToken(entityDescriptor.Entity);
            }

            if (_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                return domainEntity.ConcurrencyToken;
            }

            return default;
        }

        /// <inheritdoc/>
        public void SetConcurrencyToken(EntityDescriptor entityDescriptor, ConcurrencyToken concurrencyToken)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                ((IDomainEntity)entityDescriptor.Entity).ConcurrencyToken = concurrencyToken;
                return;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessConcurrencyToken)
            {
                accessor.SetConcurrencyToken(entityDescriptor.Entity, concurrencyToken);
                return;
            }

            if (!_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                domainEntity = new DomainEntity();
                _domainEntites.Add(entityDescriptor, domainEntity);
            }

            domainEntity.ConcurrencyToken = concurrencyToken;
        }

        /// <inheritdoc/>
        public long GetRevision(EntityDescriptor entityDescriptor)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                return ((IDomainEntity)entityDescriptor.Entity).Revision;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessRevision)
            {
                return accessor.GetRevision(entityDescriptor.Entity);
            }

            if (_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                return domainEntity.Revision;
            }

            return 0;
        }

        /// <inheritdoc/>
        public void SetRevision(EntityDescriptor entityDescriptor, long revision)
        {
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                ((IDomainEntity)entityDescriptor.Entity).Revision = revision;
                return;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessRevision)
            {
                accessor.SetRevision(entityDescriptor.Entity, revision);
                return;
            }

            if (!_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                domainEntity = new DomainEntity();
                _domainEntites.Add(entityDescriptor, domainEntity);
            }

            domainEntity.Revision = revision;
        }

        /// <inheritdoc/>
        public void CommitEvents(EntityDescriptor entityDescriptor)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                ((IDomainEntity)entityDescriptor.Entity).CommitEvents();
                return;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessEvents)
            {
                accessor.CommitEvents(entityDescriptor.Entity);
                return;
            }

            if (!_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                domainEntity = new DomainEntity();
                _domainEntites.Add(entityDescriptor, domainEntity);
            }

            domainEntity.CommitEvents();
        }

        /// <inheritdoc/>
        public DomainEventCollection GetUncommittedEvents(EntityDescriptor entityDescriptor)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                return new DomainEventCollection(((IDomainEntity)entityDescriptor.Entity).GetUncommittedEvents());
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessEvents)
            {
                return accessor.GetUncommittedEvents(entityDescriptor.Entity);
            }

            if (_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                return new DomainEventCollection(domainEntity.GetUncommittedEvents());
            }

            return default;
        }

        /// <inheritdoc/>
        public void AddEvent(EntityDescriptor entityDescriptor, DomainEvent domainEvent)
        {
            if (typeof(IDomainEntity).IsAssignableFrom(entityDescriptor.EntityType))
            {
                ((IDomainEntity)entityDescriptor.Entity).AddEvent(domainEvent);
                return;
            }

            var accessor = ConventionBasedEntityAccessor.GetAccessor(entityDescriptor.EntityType);

            if (accessor.CanAccessEvents)
            {
                accessor.AddEvent(entityDescriptor.Entity, domainEvent);
                return;
            }

            if (!_domainEntites.TryGetValue(entityDescriptor, out var domainEntity))
            {
                domainEntity = new DomainEntity();
                _domainEntites.Add(entityDescriptor, domainEntity);
            }

            domainEntity.AddEvent(domainEvent);
        }
    }
}
