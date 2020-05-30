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
    /// Represents a success entity load-result that indicates that an entity was loaded successfully.
    /// </summary>
    public interface ISuccessEntityLoadResult : ICacheableEntityLoadResult, IScopeableEnityLoadResult
    {
        /// <summary>
        /// Gets the loaded entity.
        /// </summary>
        object Entity { get; }

        /// <summary>
        /// Returns a cached copy of the current instance.
        /// </summary>
        /// <returns>A <see cref="ISuccessEntityLoadResult"/> that is cached.</returns>
        new ISuccessEntityLoadResult AsCachedResult();

        /// <summary>
        /// Scopes the current instance to the specified <see cref="IEntityStorage"/>.
        /// </summary>
        /// <param name="entityStorage">The <see cref="IEntityStorage"/> that defines the scope.</param>
        /// <returns>
        /// A <see cref="ISuccessEntityLoadResult"/> that is scoped to <paramref name="entityStorage"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityStorage"/> is <c>null</c>.
        /// </exception>
        new ISuccessEntityLoadResult ScopeTo(IEntityStorage entityStorage);

        /// <summary>
        /// Unscopes the current instance.
        /// </summary>
        /// <returns>A <see cref="ISuccessEntityLoadResult"/> that is unscoped.</returns>
        new ISuccessEntityLoadResult Unscope();

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        ICacheableEntityLoadResult ICacheableEntityLoadResult.AsCachedResult()
        {
            return AsCachedResult();
        }

        IScopeableEnityLoadResult IScopeableEnityLoadResult.ScopeTo(IEntityStorage entityStorage)
        {
            return ScopeTo(entityStorage);
        }

        IScopeableEnityLoadResult IScopeableEnityLoadResult.Unscope()
        {
            return Unscope();
        }
#endif
    }
}