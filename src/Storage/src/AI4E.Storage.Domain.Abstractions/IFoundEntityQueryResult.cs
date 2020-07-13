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
    /// <summary>
    /// Represents an entity query-result that indicates that an entity was found and loaded successfully.
    /// </summary>
    public interface IFoundEntityQueryResult : IScopeableEntityQueryResult<IFoundEntityQueryResult>
    {
        /// <summary>
        /// Gets the loaded entity.
        /// </summary>
        object Entity { get; }

        /// <summary>
        /// Returns a cached copy of the current instance.
        /// </summary>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the resulting <see cref="IEntityQueryResult"/> was loaded from cache.
        /// </param>
        /// <returns>
        /// A <see cref="IFoundEntityQueryResult"/> that reflects the value specified by <paramref name="loadedFromCache"/>.
        /// </returns>
        new IFoundEntityQueryResult AsCachedResult(bool loadedFromCache = true);

        IEntityQueryResult IEntityQueryResult.AsCachedResult(bool loadedFromCache)
        {
            return AsCachedResult(loadedFromCache);
        }

#pragma warning disable CA1033
        bool IEntityLoadResult.IsFound(out IFoundEntityQueryResult foundEntityQueryResult)
#pragma warning restore CA1033        
        {
            foundEntityQueryResult = this;
            return true;
        }
    }
}