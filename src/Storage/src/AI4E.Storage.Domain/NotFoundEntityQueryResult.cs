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
            if (loadedFromCache == LoadedFromCache)
                return this;

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
}