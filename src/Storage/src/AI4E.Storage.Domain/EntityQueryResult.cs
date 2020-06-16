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
    internal abstract class EntityQueryResult : EntityLoadResult, IEntityQueryResult
    {
        protected EntityQueryResult(
            EntityIdentifier entityIdentifier,
            bool loadedFromCache = false,
            IEntityQueryResultScope? scope = null) : base(entityIdentifier)
        {
            LoadedFromCache = loadedFromCache;
            Scope = scope;
        }

        #region Caching

        public bool LoadedFromCache { get; }

        protected abstract EntityQueryResult AsCachedResultImpl(bool loadedFromCache);

        // C# really needs covariant return types :/ https://github.com/dotnet/csharplang/issues/49
        public EntityQueryResult AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResultImpl(loadedFromCache);
        }

        IEntityQueryResult IEntityQueryResult.AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        #endregion

        #region Scope

        public IEntityQueryResultScope? Scope { get; }

        // C# really needs covariant return types :/ https://github.com/dotnet/csharplang/issues/49
        protected abstract EntityQueryResult AsScopedToImpl(IEntityQueryResultScope? scope);

        public EntityQueryResult AsScopedTo(IEntityQueryResultScope? scope)
        {
            return AsScopedToImpl(scope);
        }

        IEntityQueryResult IEntityQueryResult.AsScopedTo(IEntityQueryResultScope? scope)
        {
            return AsScopedToImpl(scope);
        }

        #endregion
    }
}