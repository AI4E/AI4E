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
    internal sealed class FoundEntityQueryResult : EntityQueryResult, IFoundEntityQueryResult
    {
        private readonly ScopeableEntity _scopebleEntity;

        public FoundEntityQueryResult(
           EntityIdentifier entityIdentifier,
           object entity,
           ConcurrencyToken concurrencyToken,
           long revision,
           bool loadedFromCache = false) : base(entityIdentifier, loadedFromCache)
        {
            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            if (!entityIdentifier.EntityType.IsInstanceOfType(entity))
                throw new ArgumentException(Resources.EntityMustBeAsignableToEntityType);

            ConcurrencyToken = concurrencyToken;
            Revision = revision;
            _scopebleEntity = new ScopeableEntity(entity);
        }

        private FoundEntityQueryResult(
           EntityIdentifier entityIdentifier,
           ScopeableEntity scopeableEntity,
           ConcurrencyToken concurrencyToken,
           long revision,
           bool loadedFromCache,
           IEntityQueryResultScope? scope) : base(entityIdentifier, loadedFromCache, scope)
        {
            _scopebleEntity = new ScopeableEntity(scopeableEntity);
            ConcurrencyToken = concurrencyToken;
            Revision = revision;
        }

        public object Entity => _scopebleEntity.GetEntity(Scope);

        public override ConcurrencyToken ConcurrencyToken { get; }

        public override long Revision { get; }

        public override string Reason => Resources.SuccessfullyLoaded;

        public override object? GetEntity(bool throwOnFailure)
        {
            return Entity;
        }

        #region Caching

        protected override EntityQueryResult AsCachedResultImpl(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

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

        protected override EntityQueryResult AsScopedToImpl(IEntityQueryResultScope? scope)
        {
            return AsScopedTo(scope);
        }

        public new FoundEntityQueryResult AsScopedTo(IEntityQueryResultScope? scope)
        {
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

        IFoundEntityQueryResult IFoundEntityQueryResult.AsScopedTo(IEntityQueryResultScope? scope)
        {
            return AsScopedTo(scope);
        }

        #endregion
    }
}