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
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="ITrackedEntityLoadResult{TLoadResult}"/>
    public class TrackedEntityQueryResult : EntityQueryResult, ITrackedEntityLoadResult<EntityQueryResult>, IScopedEntityQueryResult<EntityQueryResult>
    {
        private readonly EntityQueryResult _currentLoadResult;
        private readonly ConcurrencyToken _updatedConcurrencyToken;

        #region C'tor

        internal TrackedEntityQueryResult(
            EntityQueryResult trackedLoadResult,
            IConcurrencyTokenFactory concurrencyTokenFactory)
            : base(trackedLoadResult.EntityIdentifier,
                   trackedLoadResult.LoadedFromCache,
                   trackedLoadResult.Scope)
        {
            _currentLoadResult = TrackedLoadResult = trackedLoadResult;
            _updatedConcurrencyToken = concurrencyTokenFactory.CreateConcurrencyToken(
                trackedLoadResult.EntityIdentifier);
        }

        private TrackedEntityQueryResult(
            EntityQueryResult trackedLoadResult,
            EntityQueryResult currentLoadResult,
            ConcurrencyToken updatedConcurrencyToken)
            : base(trackedLoadResult.EntityIdentifier, currentLoadResult.LoadedFromCache, currentLoadResult.Scope)
        {
            TrackedLoadResult = trackedLoadResult;
            _currentLoadResult = currentLoadResult;
            _updatedConcurrencyToken = updatedConcurrencyToken;
        }

        #endregion

        /// <inheritdoc/>
        public EntityQueryResult TrackedLoadResult { get; }

        /// <inheritdoc />
        public override ConcurrencyToken ConcurrencyToken => _currentLoadResult.ConcurrencyToken;

        /// <inheritdoc />
        public override long Revision => _currentLoadResult.Revision;

        /// <inheritdoc />
        public override string Reason => _currentLoadResult.Reason;

        #region Tracking

        /// <inheritdoc/>
        public TrackedEntityQueryResult RecordDeleteOperation()
        {
            if (!_currentLoadResult.IsFound(out var foundEntityQueryResult))
            {
                return this;
            }

            var notFoundQueryResult = new NotFoundEntityQueryResult(
                foundEntityQueryResult.EntityIdentifier,
                loadedFromCache: false, // TODO Do we pass in the original value here or always false?
                foundEntityQueryResult.Scope);

            return new TrackedEntityQueryResult(
                TrackedLoadResult,
                notFoundQueryResult,
                _updatedConcurrencyToken);

        }

        /// <inheritdoc/>
        public TrackedEntityQueryResult RecordCreateOrUpdateOperation(object entity)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            if (_currentLoadResult.IsFound(out var foundEntityQueryResult) && foundEntityQueryResult.Entity == entity)
            {
                return this;
            }

            var foundQueryResult = new FoundEntityQueryResult(
                EntityIdentifier,
                entity,
                _updatedConcurrencyToken,
                TrackedLoadResult.Revision + 1,
                loadedFromCache: false, // TODO Do we pass in the original value here or always false?
                _currentLoadResult.Scope);

            return new TrackedEntityQueryResult(
                TrackedLoadResult,
                foundQueryResult,
                _updatedConcurrencyToken);
        }

        ITrackedEntityLoadResult<EntityQueryResult> ITrackedEntityLoadResult<EntityQueryResult>.RecordDeleteOperation()
        {
            return RecordDeleteOperation();
        }

        ITrackedEntityLoadResult<EntityQueryResult> ITrackedEntityLoadResult<EntityQueryResult>.RecordCreateOrUpdateOperation(object entity)
        {
            return RecordCreateOrUpdateOperation(entity);
        }

        /// <inheritdoc/>
        public EntityQueryResult ApplyRecordedOperations()
        {
            return _currentLoadResult;
        }

        ITrackedEntityLoadResult<EntityQueryResult> ITrackableEntityLoadResult<EntityQueryResult>.AsTracked(
            IConcurrencyTokenFactory concurrencyTokenFactory)
        {
            return this;
        }

        /// <inheritdoc/>
        public override TrackedEntityQueryResult AsTracked(IConcurrencyTokenFactory concurrencyTokenFactory)
        {
            if (concurrencyTokenFactory is null)
                throw new ArgumentNullException(nameof(concurrencyTokenFactory));

            return this;
        }

        #endregion

        #region Caching

        /// <inheritdoc/>
        protected override EntityQueryResult AsCachedResultImpl(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        /// <inheritdoc cref="IEntityQueryResult.AsCachedResult(bool)"/>
        public new TrackedEntityQueryResult AsCachedResult(bool loadedFromCache)
        {
            if (LoadedFromCache == loadedFromCache)
                return this;

            return new TrackedEntityQueryResult(
                TrackedLoadResult,
                _currentLoadResult.AsCachedResult(loadedFromCache),
                _updatedConcurrencyToken);
        }

        #endregion

        #region Scope

        /// <inheritdoc/>
        protected override EntityQueryResult AsScopedToImpl(IEntityQueryResultScope scope)
        {
            return AsScopedTo(scope);
        }

        /// <inheritdoc cref="IScopeableEntityQueryResult{IFoundEntityQueryResult}.AsScopedTo(IEntityQueryResultScope)"/>
        public new TrackedEntityQueryResult AsScopedTo(IEntityQueryResultScope scope)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (scope == Scope)
                return this;

            return new TrackedEntityQueryResult(
                TrackedLoadResult,
                _currentLoadResult.AsScopedTo(scope),
                _updatedConcurrencyToken);
        }

        #endregion

        /// <inheritdoc />
        public override bool IsFound([NotNullWhen(true)] out FoundEntityQueryResult? foundEntityQueryResult)
        {
            return _currentLoadResult.IsFound(out foundEntityQueryResult);
        }
    }
}