/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Collections.Generic;
using System.Threading;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection executor that is used to execute single projections.
    /// </summary>
    public interface IProjectionExecutor
    {
        /// <summary>
        /// Gets the <see cref="IProjectionProvider"/> that is used to load projections.
        /// </summary>
        IProjectionProvider ProjectionProvider { get; }

        /// <summary>
        /// Asynchronously executed a single projection.
        /// </summary>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="entity">The entity.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that enumerated the projection results.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entityType"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if either <paramref name="entity"/> is not convertible to the type specified by <paramref name="entityType"/> or
        /// <paramref name="entityType"/> specifies an invalid type or <paramref name="entity"/> is an instance of an invalid type.
        /// </exception>
        IAsyncEnumerable<IProjectionResult> ExecuteProjectionAsync(
            Type entityType,
            object entity,
            IServiceProvider serviceProvider,
            CancellationToken cancellation = default);
    }
}
