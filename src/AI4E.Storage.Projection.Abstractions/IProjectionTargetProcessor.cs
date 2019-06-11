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
using System.Threading.Tasks;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents a projection target processor that is used to manage projection targets and metadata.
    /// </summary>
    public interface IProjectionTargetProcessor
    {
        /// <summary>
        /// Gets a descriptor for the projected source.
        /// </summary>
        ProjectionSourceDescriptor ProjectedSource { get; }

        /// <summary>
        /// Asynchronously retrieves a collection of projection sources that are dependents of the projected source.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the collection of dependents.
        /// </returns>
        ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously retrieves the metadata of the projection.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the projection metadata.
        /// </returns>
        ValueTask<ProjectionMetadata> GetMetadataAsync(CancellationToken cancellation);

        /// <summary>
        /// Asynchronously updates the projection metadata.
        /// </summary>
        /// <param name="metadata">The desired projection metadata.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="metadata"/> is <c>default</c>.</exception>
        ValueTask UpdateAsync(ProjectionMetadata metadata, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously commits the recorded changes and
        /// returns a boolean value indicating whether the operation suceeded.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains a boolean value indicating whether the operation succeeded.
        /// </returns>
        /// <remarks>
        /// When the operation fails, the internal state of the object must not be reset but still reflect the same state
        /// then before the operation. The processor has to guarantee in this case that no changes are written to an underlying (durable) store.
        /// </remarks>
        ValueTask<bool> CommitAsync(CancellationToken cancellation);

        /// <summary>
        /// Asynchronously updates a target with the specified projection result.
        /// </summary>
        /// <param name="projectionResult">The projection result.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="projectionResult"/> is <c>null</c>.</exception>
        ValueTask UpdateTargetAsync(
            IProjectionResult projectionResult,
            CancellationToken cancellation);

        /// <summary>
        /// Asynchronously removed a target.
        /// </summary>
        /// <param name="target">The target to remove.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="target"/> is <c>default</c>.</exception>
        ValueTask RemoveTargetAsync(
            ProjectionTargetDescriptor target,
            CancellationToken cancellation);

        /// <summary>
        /// Clears the internal state of the processor and reverts all recorded changes.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// Represents a factory that can be used to create instances of <see cref="IProjectionTargetProcessor"/>.
    /// </summary>
    public interface IProjectionTargetProcessorFactory
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="projectedSource">A descriptor for the projected source.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> that shall be used to load services.</param>
        /// <returns>The created instance of type <see cref="IProjectionTargetProcessor"/>.</returns>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="projectedSource"/> is <c>default</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is <c>null</c>.</exception>
        IProjectionTargetProcessor CreateInstance(ProjectionSourceDescriptor projectedSource, IServiceProvider serviceProvider);
    }
}
