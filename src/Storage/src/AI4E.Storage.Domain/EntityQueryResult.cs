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
using AI4E.Utils;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityQueryResult"/>
    public abstract class EntityQueryResult
        : EntityLoadResult, IEntityQueryResult, IScopedEntityQueryResult<EntityQueryResult>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EntityQueryResult"/> class in a derived type.
        /// </summary>
        /// <param name="entityIdentifier">The identifier of the entity.</param>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the entity query-result was loaded from cache.
        /// </param>
        /// <param name="scope">The query-result scope that the query-result is scoped to.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scope"/> is <c>null</c>.</exception>
        protected EntityQueryResult(
            EntityIdentifier entityIdentifier,
            bool loadedFromCache,
            IEntityQueryResultScope scope) : base(entityIdentifier)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            LoadedFromCache = loadedFromCache;
            Scope = scope;
        }

        #region Caching

        /// <inheritdoc/>
        public bool LoadedFromCache { get; }

        // TODO:  C# really needs covariant return types :/ https://github.com/dotnet/csharplang/issues/49

        /// <summary>
        /// Returns a cached copy of the current instance.
        /// </summary>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the resulting <see cref="EntityQueryResult"/> was loaded from cache.
        /// </param>
        /// <returns>
        /// A <see cref="EntityQueryResult"/> that reflects the value specified by <paramref name="loadedFromCache"/>.
        /// </returns>
        protected abstract EntityQueryResult AsCachedResultImpl(bool loadedFromCache);

        /// <inheritdoc cref="IEntityQueryResult.AsCachedResult(bool)"/>
        public EntityQueryResult AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResultImpl(loadedFromCache);
        }

        IEntityQueryResult IEntityQueryResult.AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        #endregion

        #region Scoping

        // We scope all query-result in order not to lose the scope while tracking. This is necessary because, when we
        // create an entity, the found query-result is created from a not-found query result (that would not have a 
        // scope otherwise) and there the entity will not get scoped and not copied when inserted into the cache
        // of the storage engine on commit.

        /// <inheritdoc/>
        public IEntityQueryResultScope Scope { get; }

        // TODO: C# really needs covariant return types :/ https://github.com/dotnet/csharplang/issues/49

        /// <summary>
        /// Returns an instance of <see cref="EntityQueryResult"/> representing the current 
        /// scope-able entity query-result scoped to the specified <see cref="IEntityQueryResultScope"/>.
        /// </summary>
        /// <param name="scope">The <see cref="IEntityQueryResultScope"/> that defines the scope.</param>
        /// <returns>
        /// A <see cref="EntityQueryResult"/> that is scoped to <paramref name="scope"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown of <paramref name="scope"/> is <c>null</c>.</exception>
        protected abstract EntityQueryResult AsScopedToImpl(IEntityQueryResultScope scope);

        /// <inheritdoc cref="IScopeableEntityQueryResult{EntityQueryResult2}.AsScopedTo(IEntityQueryResultScope)"/>
        public EntityQueryResult AsScopedTo(IEntityQueryResultScope scope)
        {
            return AsScopedToImpl(scope);
        }

        IScopedEntityQueryResult<EntityQueryResult> IScopeableEntityQueryResult<EntityQueryResult>.AsScopedTo(
            IEntityQueryResultScope scope)
        {
            return AsScopedTo(scope);
        }

#pragma warning disable CA1033
        EntityQueryResult IScopedEntityQueryResult<EntityQueryResult>.ToQueryResult()
#pragma warning restore CA1033
        {
            return this;
        }

        /// <inheritdoc/>
        public override bool IsScopeable<TQueryResult>(
            [NotNullWhen(true)] out IScopeableEntityQueryResult<TQueryResult>? scopeableEntityQueryResult)
        {
            scopeableEntityQueryResult = this as IScopeableEntityQueryResult<TQueryResult>;
            return scopeableEntityQueryResult != null;
        }

        #endregion

        #region Tracking

        /// <inheritdoc cref="ITrackableEntityLoadResult{IEntityQueryResult}.AsTracked(IEntityConcurrencyTokenFactory)"/>
        public TrackedEntityQueryResult AsTracked(
            IEntityConcurrencyTokenFactory concurrencyTokenFactory)
        {
            if (concurrencyTokenFactory is null)
                throw new ArgumentNullException(nameof(concurrencyTokenFactory));

            return new TrackedEntityQueryResult(this, concurrencyTokenFactory);
        }

        ITrackedEntityLoadResult<IEntityQueryResult> ITrackableEntityLoadResult<IEntityQueryResult>.AsTracked(
            IEntityConcurrencyTokenFactory concurrencyTokenFactory)
        {
            return AsTracked(concurrencyTokenFactory);
        }

        /// <inheritdoc/>
        public sealed override bool IsTrackable<TLoadResult>(
            [NotNullWhen(true)] out ITrackableEntityLoadResult<TLoadResult>? trackableEntityLoadResult)
        {
            trackableEntityLoadResult = this as ITrackableEntityLoadResult<TLoadResult>;
            return trackableEntityLoadResult != null;
        }

        #endregion
    }

    /// <inheritdoc cref="ITrackedEntityLoadResult{TLoadResult}"/>
    public class TrackedEntityQueryResult : EntityQueryResult, ITrackedEntityLoadResult<EntityQueryResult>, IScopedEntityQueryResult<EntityQueryResult>
    {
        private readonly EntityQueryResult _currentLoadResult;
        private readonly ConcurrencyToken _updatedConcurrencyToken;

        #region C'tor

        internal TrackedEntityQueryResult(
            EntityQueryResult trackedLoadResult,
            IEntityConcurrencyTokenFactory concurrencyTokenFactory)
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
            IEntityConcurrencyTokenFactory concurrencyTokenFactory)
        {
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

    /// <inheritdoc cref="IFoundEntityQueryResult" />
    public sealed class FoundEntityQueryResult : EntityQueryResult, IFoundEntityQueryResult, IScopedEntityQueryResult<IFoundEntityQueryResult>
    {
        private readonly ScopeableEntity _scopebleEntity;

        /// <summary>
        /// Creates a new instance of the <see cref="FoundEntityQueryResult"/> type.
        /// </summary>
        /// <param name="entityIdentifier">The identifier of the entity.</param>
        /// <param name="entity">The entity that was found.</param>
        /// <param name="concurrencyToken">The concurrency token of the found entity.</param>
        /// <param name="revision">The revision of the found entity.</param>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the entity query-result was loaded from cache.
        /// </param>
        /// <param name="scope">
        /// The query-result scope that the query-result is scoped to and that owns the found entity.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entity"/> or <paramref name="scope"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if 
        /// <paramref name="entity"/> is not of entity-type specified by <paramref name="entityIdentifier"/> or 
        /// assignable to it 
        /// -- OR --
        /// <paramref name="entity"/> is a delegate or a value-type.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="revision"/> is negative.</exception>
        public FoundEntityQueryResult(
           EntityIdentifier entityIdentifier,
           object entity,
           ConcurrencyToken concurrencyToken,
           long revision,
           bool loadedFromCache,
           IEntityQueryResultScope scope) : base(entityIdentifier, loadedFromCache, scope)
        {
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            EntityValidationHelper.Validate(EntityIdentifier.EntityType, entity, validateEntityType: false);

            ConcurrencyToken = concurrencyToken;
            Revision = revision;
            _scopebleEntity = new ScopeableEntity(scope, entity);
        }

        private FoundEntityQueryResult(
           EntityIdentifier entityIdentifier,
           ScopeableEntity scopeableEntity,
           ConcurrencyToken concurrencyToken,
           long revision,
           bool loadedFromCache,
           IEntityQueryResultScope scope) : base(entityIdentifier, loadedFromCache, scope)
        {
            _scopebleEntity = scopeableEntity;
            ConcurrencyToken = concurrencyToken;
            Revision = revision;
        }

        /// <inheritdoc />
        public object Entity => _scopebleEntity.GetEntity(Scope);

        /// <inheritdoc />
        public override ConcurrencyToken ConcurrencyToken { get; }

        /// <inheritdoc />
        public override long Revision { get; }

        /// <inheritdoc />
        public override string Reason => Resources.SuccessfullyLoaded;

        /// <inheritdoc />
        public override bool IsFound(out FoundEntityQueryResult foundEntityQueryResult)
        {
            foundEntityQueryResult = this;
            return true;
        }

        #region Caching

        /// <inheritdoc/>
        protected override EntityQueryResult AsCachedResultImpl(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        /// <inheritdoc cref="IFoundEntityQueryResult.AsCachedResult(bool)"/>
        public new FoundEntityQueryResult AsCachedResult(bool loadedFromCache)
        {
            if (LoadedFromCache == loadedFromCache)
                return this;

            return new FoundEntityQueryResult(
                EntityIdentifier,

                _scopebleEntity,
                ConcurrencyToken,
                Revision,
                loadedFromCache,
                Scope);
        }

        IFoundEntityQueryResult IFoundEntityQueryResult.AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        #endregion

        #region Scope

        /// <inheritdoc/>
        protected override EntityQueryResult AsScopedToImpl(IEntityQueryResultScope scope)
        {
            return AsScopedTo(scope);
        }

        /// <inheritdoc cref="IScopeableEntityQueryResult{IFoundEntityQueryResult}.AsScopedTo(IEntityQueryResultScope)"/>
        public new FoundEntityQueryResult AsScopedTo(IEntityQueryResultScope scope)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (scope == Scope)
                return this;

            return new FoundEntityQueryResult(
                EntityIdentifier,
                _scopebleEntity,
                ConcurrencyToken,
                Revision,
                LoadedFromCache,
                scope);
        }

        IScopedEntityQueryResult<IFoundEntityQueryResult> IScopeableEntityQueryResult<IFoundEntityQueryResult>.AsScopedTo(
            IEntityQueryResultScope scope)
        {
            return AsScopedTo(scope);
        }

        IFoundEntityQueryResult IScopedEntityQueryResult<IFoundEntityQueryResult>.ToQueryResult()
        {
            return this;
        }

        #endregion
    }

    /// <summary>
    /// Represents an entity load-result that indicates that an entity was not loaded as it cannot be found.
    /// </summary>
    public sealed class NotFoundEntityQueryResult : EntityQueryResult
    {
        /// <summary>
        /// Creates a new instance of the <see cref="NotFoundEntityQueryResult"/> type.
        /// </summary>
        /// <param name="entityIdentifier">The identifier of the entity.</param>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the entity query-result was loaded from cache.
        /// </param>
        /// <param name="scope">The query-result scope that the query-result is scoped to.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scope"/> is <c>null</c>.</exception>
        public NotFoundEntityQueryResult(
            EntityIdentifier entityIdentifier,
            bool loadedFromCache,
            IEntityQueryResultScope scope) : base(entityIdentifier, loadedFromCache, scope)
        { }

        /// <inheritdoc/>
        public override string Reason => Resources.NotFound;

        #region Caching

        /// <inheritdoc/>
        protected override EntityQueryResult AsCachedResultImpl(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        /// <inheritdoc cref="IEntityQueryResult.AsCachedResult(bool)"/>
        public new NotFoundEntityQueryResult AsCachedResult(bool loadedFromCache)
        {
            return new NotFoundEntityQueryResult(EntityIdentifier, loadedFromCache, Scope);
        }

        #endregion

        #region Scope

        /// <inheritdoc/>
        protected override EntityQueryResult AsScopedToImpl(IEntityQueryResultScope scope)
        {
            return AsScopedTo(scope);
        }

        /// <inheritdoc cref="IScopeableEntityQueryResult{IQueryResult}.AsScopedTo(IEntityQueryResultScope)"/>
        public new NotFoundEntityQueryResult AsScopedTo(IEntityQueryResultScope scope)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            if (scope == Scope)
                return this;

            return new NotFoundEntityQueryResult(
                EntityIdentifier,
                LoadedFromCache,
                scope);
        }

        #endregion
    }

    /// <summary>
    /// Represents the global entity query-result scope.
    /// </summary>
    public sealed class GlobalEntityQueryResultScope : IEntityQueryResultScope
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="GlobalEntityQueryResultScope"/> type.
        /// </summary>
        public static GlobalEntityQueryResultScope Instance { get; } = new GlobalEntityQueryResultScope();

        private GlobalEntityQueryResultScope() { }

        /// <inheritdoc/>
        public object ScopeEntity(object originalEntity)
        {
            if (originalEntity is null)
                throw new ArgumentNullException(nameof(originalEntity));

            return ObjectExtension.DeepClone(originalEntity)!;
        }
    }
}