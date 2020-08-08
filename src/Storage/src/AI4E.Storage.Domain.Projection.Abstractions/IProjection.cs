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
    /// Represents a projection.
    /// </summary>
    public interface IProjection
    {
        /// <summary>
        /// Asynchronously projects the specified source object.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that enumerates the projection results.
        /// </returns>
        IAsyncEnumerable<object> ProjectAsync(object source, CancellationToken cancellation = default);

        /// <summary>
        /// Gets the type of projection source.
        /// </summary>
        Type SourceType { get; }

        /// <summary>
        /// Gets the type of projection target.
        /// </summary>
        Type TargetType { get; }
    }

    /// <summary>
    /// Represents a projection with the specified source and target types.
    /// </summary>
    /// <typeparam name="TSource">The type of projection source.</typeparam>
    /// <typeparam name="TTarget">The type of projection target.</typeparam>
    public interface IProjection<TSource, TTarget> : IProjection
        where TSource : class
        where TTarget : class
    {
        /// <summary>
        /// Asynchronously projects the specified source object.
        /// </summary>
        /// <param name="source">The source object.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that enumerates the projection results.
        /// </returns>
        IAsyncEnumerable<TTarget> ProjectAsync(TSource source, CancellationToken cancellation = default);

        Type IProjection.SourceType => typeof(TSource);
        Type IProjection.TargetType => typeof(TTarget);

        IAsyncEnumerable<object> IProjection.ProjectAsync(object source, CancellationToken cancellation)
        {
            return ProjectAsync(source as TSource, cancellation);
        }
    }
}
