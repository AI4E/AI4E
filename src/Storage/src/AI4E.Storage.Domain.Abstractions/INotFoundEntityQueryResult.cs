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
    /// Represents a load-result indicating that an entity cannot be loaded due to non-existence.
    /// </summary>
    public interface INotFoundEntityQueryResult : IEntityQueryResult
    {
        /// <summary>
        /// Returns a cached copy of the current instance.
        /// </summary>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the resulting <see cref="IEntityQueryResult"/> was loaded from cache.
        /// </param>
        /// <returns>
        /// A <see cref="INotFoundEntityQueryResult"/> that reflects the value specified by <paramref name="loadedFromCache"/>.
        /// </returns>
        new INotFoundEntityQueryResult AsCachedResult(bool loadedFromCache = true);

        /// <summary>
        /// Returns a copy of the current instance scoped to the specified <see cref="IEntityQueryResultScope"/>.
        /// </summary>
        /// <param name="scope">The <see cref="IEntityQueryResultScope"/> that defines the scope.</param>
        /// <returns>
        /// A <see cref="INotFoundEntityQueryResult"/> that is scoped to <paramref name="scope"/>.
        /// </returns>
        new INotFoundEntityQueryResult AsScopedTo(IEntityQueryResultScope? scope);

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        IEntityQueryResult IEntityQueryResult.AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

        IEntityQueryResult IEntityQueryResult.AsScopedTo(IEntityQueryResultScope? scope)
        {
            return AsScopedTo(scope);
        }

#endif
    }
}