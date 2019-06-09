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

// TODO: Move me to Projections.Abstractions, when we can remove the dependency on IScopedDatabase

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Projection
{
    public interface IProjectionTargetProcessor
    {
        ProjectionSourceDescriptor ProjectedSource { get; }

        ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation);

        ValueTask<SourceMetadata> GetMetadataAsync(CancellationToken cancellation);
        ValueTask UpdateAsync(SourceMetadata metadata, CancellationToken cancellation);
        ValueTask<bool> CommitAsync(CancellationToken cancellation);

        ValueTask UpdateEntityToProjectionAsync(
            IProjectionResult projectionResult,
            bool addToTargetMetadata,
            CancellationToken cancellation);

        ValueTask RemoveEntityFromProjectionAsync(
            ProjectionTargetDescriptor removedProjection,
            CancellationToken cancellation);

        void Clear();
    }

    public interface IProjectionTargetProcessorFactory
    {
        IProjectionTargetProcessor CreateInstance(ProjectionSourceDescriptor projectedSource);
    }
}
