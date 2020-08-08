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

namespace AI4E.Storage.Domain
{
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
}