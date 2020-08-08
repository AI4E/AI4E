/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Threading.Tasks;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection source processor that used to manage projection sources.
    /// </summary>
    public interface IProjectionSourceProcessor
    {
        /// <summary>
        /// Gets a descriptor for the projected source.
        /// </summary>
        EntityIdentifier ProjectedSource { get; }

        /// <summary>
        /// Asynchronously retrieves a projection source.
        /// </summary>
        /// <param name="projectionSource">The descriptor of the projection source.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Object}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the projection source, or <c>null</c> if the source was not found.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="projectionSource"/> is <c>default.</c></exception>
        ValueTask<object?> GetSourceAsync(
            EntityIdentifier projectionSource,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously retrieves the revision of a projection source.
        /// </summary>
        /// <param name="projectionSource">The descriptor of the projection source.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{Long}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the revision of the projection source, or <c>0</c> if the source was not found.
        /// </returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="projectionSource"/> is <c>default.</c></exception>
        ValueTask<long> GetSourceRevisionAsync(
            EntityIdentifier projectionSource,
            long? expectedMinRevision = default,
            CancellationToken cancellation = default);

        /// <summary>
        /// Gets the dependencies of the projected source.
        /// </summary>
        IEnumerable<EntityDependency> Dependencies { get; }
    }

    /// <summary>
    /// Represents a factory that can be used to create instances of <see cref="IProjectionSourceProcessor"/>.
    /// </summary>
    public interface IProjectionSourceProcessorFactory
    {
        /// <summary>
        /// Creates a new <see cref="IProjectionSourceProcessor"/>.
        /// </summary>
        /// <param name="projectedSource">A descriptor for the projected source.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> that shall be used to load services.</param>
        /// <returns>The created instance of type <see cref="IProjectionSourceProcessor"/>.</returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="projectedSource"/> is <c>default</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is <c>null</c>.</exception>
        IProjectionSourceProcessor CreateInstance(EntityIdentifier projectedSource, IServiceProvider serviceProvider);
    }
}
