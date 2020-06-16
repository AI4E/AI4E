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

namespace AI4E.Storage.Domain
{
    internal sealed class NotFoundEntityQueryResult : EntityQueryResult, INotFoundEntityQueryResult
    {
        public NotFoundEntityQueryResult(EntityIdentifier entityIdentifier, bool loadedFromCache = false)
            : base(entityIdentifier, loadedFromCache)
        { }

        private NotFoundEntityQueryResult(
            EntityIdentifier entityIdentifier,
            bool loadedFromCache,
            IEntityQueryResultScope? scope)
            : base(entityIdentifier, loadedFromCache, scope)
        { }

        public override string Reason => Resources.NotFound;

        #region Caching

        protected override EntityQueryResult AsCachedResultImpl(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        public new NotFoundEntityQueryResult AsCachedResult(bool loadedFromCache)
        {
            return new NotFoundEntityQueryResult(EntityIdentifier, loadedFromCache);
        }

        INotFoundEntityQueryResult INotFoundEntityQueryResult.AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        #endregion

        #region Scope

        protected override EntityQueryResult AsScopedToImpl(IEntityQueryResultScope? scope)
        {
            return AsScopedTo(scope);
        }

        public new NotFoundEntityQueryResult AsScopedTo(IEntityQueryResultScope? scope)
        {
            if (scope == Scope)
                return this;

            return new NotFoundEntityQueryResult(
                EntityIdentifier,
                LoadedFromCache,
                scope);
        }

        INotFoundEntityQueryResult INotFoundEntityQueryResult.AsScopedTo(IEntityQueryResultScope? scope)
        {
            return AsScopedTo(scope);
        }

        #endregion
    }
}