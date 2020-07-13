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

using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a cache-able entity load result.
    /// </summary>
    public interface IEntityQueryResult : ITrackableEntityLoadResult<IEntityQueryResult>
    {
#pragma warning disable CA1033
        bool IEntityLoadResult.IsVerificationFailed(
#pragma warning restore CA1033
            [NotNullWhen(true)] out IEntityVerificationResult? verificationEntityResult)
        {
            verificationEntityResult = null;
            return false;
        }

        /// <summary>
        /// Gets a boolean value indicating whether the <see cref="IEntityQueryResult"/> was loaded from cache 
        /// or freshly loaded from the underlying store.
        /// </summary>
        bool LoadedFromCache { get; } // TODO: Specify whether loaded from cache shall be true for a tracked entity with changes where the underlying query-result was loaded from cache.

        /// <summary>
        /// Returns a cached copy of the current instance.
        /// </summary>
        /// <param name="loadedFromCache">
        /// A boolean value indicating whether the resulting <see cref="IEntityQueryResult"/> was loaded from cache.
        /// </param>
        /// <returns>
        /// A <see cref="IEntityQueryResult"/> that reflects the value specified by <paramref name="loadedFromCache"/>.
        /// </returns>
        IEntityQueryResult AsCachedResult(bool loadedFromCache = true);
    }
}