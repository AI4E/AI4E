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
    internal sealed class SuccessEntityLoadResult : CacheableEntityLoadResult, ISuccessEntityLoadResult
    {
        private readonly ScopebleEntity _scopebleEntity;

        public SuccessEntityLoadResult(
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

            if (!entityIdentifier.EntityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException(Resources.EntityMustBeAsignableToEntityType);

            ConcurrencyToken = concurrencyToken;
            Revision = revision;
            _scopebleEntity = new ScopebleEntity(entity);
        }

        private SuccessEntityLoadResult(
           EntityIdentifier entityIdentifier,
           ScopebleEntity scopeableEntity,
           ConcurrencyToken concurrencyToken,
           long revision,
           bool loadedFromCache,
           IEntityStorage? scope) : base(entityIdentifier, loadedFromCache)
        {
            _scopebleEntity = new ScopebleEntity(scopeableEntity);
            ConcurrencyToken = concurrencyToken;
            Revision = revision;
            Scope = scope;
        }

        public object Entity => _scopebleEntity.GetEntity(Scope);

        public override ConcurrencyToken ConcurrencyToken { get; }

        public override long Revision { get; }

        #region AsCachedResult

        protected override CacheableEntityLoadResult AsCachedResultImpl()
        {
            return AsCachedResult();
        }

        public new SuccessEntityLoadResult AsCachedResult()
        {
            return new SuccessEntityLoadResult(
                EntityIdentifier,
                _scopebleEntity,
                ConcurrencyToken,
                Revision,
                loadedFromCache: true,
                Scope);
        }

        ISuccessEntityLoadResult ISuccessEntityLoadResult.AsCachedResult()
        {
            return AsCachedResult();
        }

        #endregion

        #region Scope

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        IScopeableEnityLoadResult IScopeableEnityLoadResult.ScopeTo(IEntityStorage entityStorage)
        {
            return ScopeTo(entityStorage);
        }

        IScopeableEnityLoadResult IScopeableEnityLoadResult.Unscope()
        {
            return Unscope();
        }
#endif

        ISuccessEntityLoadResult ISuccessEntityLoadResult.ScopeTo(IEntityStorage entityStorage)
        {
            return ScopeTo(entityStorage);
        }

        ISuccessEntityLoadResult ISuccessEntityLoadResult.Unscope()
        {
            return Unscope();
        }

        public SuccessEntityLoadResult ScopeTo(IEntityStorage entityStorage)
        {
            if (entityStorage is null)
                throw new ArgumentNullException(nameof(entityStorage));

            return new SuccessEntityLoadResult(
                EntityIdentifier,
                _scopebleEntity,
                ConcurrencyToken,
                Revision,
                LoadedFromCache,
                scope: entityStorage);
        }

        public SuccessEntityLoadResult Unscope()
        {
            return new SuccessEntityLoadResult(
                EntityIdentifier,
                _scopebleEntity,
                ConcurrencyToken,
                Revision,
                LoadedFromCache,
                scope: null);
        }

        public IEntityStorage? Scope { get; }

        #endregion

        public override string Reason => Resources.SuccessfullyLoaded;
    }
}