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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a factory for scoped entity storage instances.
    /// </summary>
    public interface IEntityStorageFactory
    {
        /// <summary>
        /// Creates a scoped entity storage instances.
        /// </summary>
        /// <returns>The created entity storage.</returns>
        IEntityStorage CreateEntityStorage();

        /// <summary>
        /// Creates a scoped entity storage instance within the specified scope.
        /// </summary>
        /// <param name="scope">The service scope.</param>
        /// <returns>The created entity storage.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="scope"/> is <c>null</c>.</exception>
        IEntityStorage CreateEntityStorage(IServiceScope scope);
    }
}
