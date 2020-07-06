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
    /// <inheritdoc cref="IUnitOfWorkEntry{TLoadResult}"/>
    public sealed class UnitOfWorkEntry<TLoadResult> : IUnitOfWorkEntry<TLoadResult>
        where TLoadResult : class, IEntityLoadResult
    {
        private readonly int _epoch;

        internal UnitOfWorkEntry(
            UnitOfWork<TLoadResult> unitOfWork, 
            int epoch,
            ITrackedEntityLoadResult<TLoadResult> loadResult)
        {
            UnitOfWork = unitOfWork;
            _epoch = epoch;
            EntityLoadResult = loadResult;
        }

        private UnitOfWorkEntry(
            UnitOfWork<TLoadResult> unitOfWork,
            int epoch,
            ITrackedEntityLoadResult<TLoadResult> loadResult,
            DomainEventCollection recordedDomainEvents)
        {
            UnitOfWork = unitOfWork;
            _epoch = epoch;
            EntityLoadResult = loadResult;
            RecordedDomainEvents = recordedDomainEvents;
            IsModified = true;
        }

        /// <summary>
        /// Gets the unit of work that the entry is tracked by.
        /// </summary>
        public UnitOfWork<TLoadResult> UnitOfWork { get; }

        /// <inheritdoc/>
        public ITrackedEntityLoadResult<TLoadResult> EntityLoadResult { get; }

        /// <inheritdoc/>
        public bool IsModified { get; }

        /// <inheritdoc/>
        public DomainEventCollection RecordedDomainEvents { get; }

        /// <inheritdoc cref="IUnitOfWorkEntry{TLoadResult}.Delete(DomainEventCollection)"/>
        public UnitOfWorkEntry<TLoadResult> Delete(DomainEventCollection domainEvents)
        {
            var combinedDomainEvents = RecordedDomainEvents.Concat(domainEvents);
            var trackedLoadResult = EntityLoadResult.RecordDeleteOperation();

            if (trackedLoadResult == EntityLoadResult && combinedDomainEvents == RecordedDomainEvents)
            {
                return this;
            }

            return Update(combinedDomainEvents, trackedLoadResult);
        }

        IUnitOfWorkEntry<TLoadResult> IUnitOfWorkEntry<TLoadResult>.Delete(DomainEventCollection domainEvents)
        {
            return Delete(domainEvents);
        }

        /// <inheritdoc cref="IUnitOfWorkEntry{TLoadResult}.CreateOrUpdate(object, DomainEventCollection)"/>
        public UnitOfWorkEntry<TLoadResult> CreateOrUpdate(object entity, DomainEventCollection domainEvents)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            var combinedDomainEvents = RecordedDomainEvents.Concat(domainEvents);
            var trackedLoadResult = EntityLoadResult.RecordCreateOrUpdateOperation(entity);

            if (trackedLoadResult == EntityLoadResult && combinedDomainEvents == RecordedDomainEvents)
            {
                return this;
            }

            return Update(combinedDomainEvents, trackedLoadResult);
        }

        IUnitOfWorkEntry<TLoadResult> IUnitOfWorkEntry<TLoadResult>.CreateOrUpdate(
            object entity, DomainEventCollection domainEvents)
        {
            return CreateOrUpdate(entity, domainEvents);
        }

        private UnitOfWorkEntry<TLoadResult> Update(
            DomainEventCollection combinedDomainEvents,
            ITrackedEntityLoadResult<TLoadResult> trackedLoadResult)
        {
            var result = new UnitOfWorkEntry<TLoadResult>(UnitOfWork, _epoch, trackedLoadResult, combinedDomainEvents);
            UnitOfWork.UpdateEntry(result, _epoch);
            return result;
        }
    }
}
