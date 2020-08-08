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
        /// Asynchronously projects the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that enumerates the projection results.
        /// </returns>
        IAsyncEnumerable<object> ProjectAsync(object entity, CancellationToken cancellation = default);

        /// <summary>
        /// Gets the type of entity.
        /// </summary>
        Type EntityType { get; }

        /// <summary>
        /// Gets the type of projection target.
        /// </summary>
        Type TargetType { get; }
    }

    /// <summary>
    /// Represents a projection with the specified entity and target types.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <typeparam name="TTarget">The type of projection target.</typeparam>
    public interface IProjection<TEntity, TTarget> : IProjection
        where TEntity : class
        where TTarget : class
    {
        /// <summary>
        /// Asynchronously projects the specified entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncEnumerable{T}"/> that enumerates the projection results.
        /// </returns>
        IAsyncEnumerable<TTarget> ProjectAsync(TEntity entity, CancellationToken cancellation = default);

#pragma warning disable CA1033
        Type IProjection.EntityType => typeof(TEntity);

        Type IProjection.TargetType => typeof(TTarget);
#pragma warning restore CA1033

        IAsyncEnumerable<object> IProjection.ProjectAsync(object entity, CancellationToken cancellation)
        {
            return ProjectAsync(entity as TEntity, cancellation);
        }
    }
}
