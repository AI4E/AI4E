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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Projection
{
    // A projection engine that is scoped in the projection source (type and id specified via ProjectionSourceDescriptor)
    internal readonly struct SourceProjector
    {
        private readonly ProjectionSourceDescriptor _sourceDescriptor;
        private readonly IProjectionExecutor _projector;
        private readonly IDatabase _database;
        private readonly IServiceProvider _serviceProvider;

        private readonly IProjectionTargetProcessor _targetProcessor;

        public SourceProjector( // TODO: Inject an instance of IProjectionTargetProcessorFactory
            in ProjectionSourceDescriptor sourceDescriptor,
            IProjectionExecutor projector,
            IDatabase database,
            IServiceProvider serviceProvider)
        {
            Debug.Assert(sourceDescriptor != default);
            Debug.Assert(projector != null);
            Debug.Assert(sourceDescriptor != default);
            Debug.Assert(database != null);
            Debug.Assert(serviceProvider != null);

            _sourceDescriptor = sourceDescriptor;
            _projector = projector;
            _database = database;
            _serviceProvider = serviceProvider;

            _targetProcessor = new ProjectionTargetProcessor(_sourceDescriptor, _database);
        }

        // Projects the source entity and returns the descriptors of all dependent source entities. 
        public async Task<IEnumerable<ProjectionSourceDescriptor>> ProjectAsync(CancellationToken cancellation)
        {
            IEnumerable<ProjectionSourceDescriptor> dependents;

            do
            {
                await ProjectCoreAsync(cancellation);
                dependents = await _targetProcessor.GetDependentsAsync(cancellation);
            }
            while (!await _targetProcessor.CommitAsync(cancellation));

            return dependents;
        }

        private async Task ProjectCoreAsync(CancellationToken cancellation)
        {
            _targetProcessor.Clear();

            using var scope = _serviceProvider.CreateScope();
            var scopedServiceProvider = scope.ServiceProvider;
            var projectionSourceLoader = scopedServiceProvider.GetRequiredService<IProjectionSourceLoader>();
            var updateNeeded = await CheckUpdateNeededAsync(projectionSourceLoader, cancellation);

            if (!updateNeeded)
                return;

            var source = await projectionSourceLoader.GetSourceAsync(_sourceDescriptor, bypassCache: false, cancellation);
            var sourceRevision = await projectionSourceLoader.GetSourceRevisionAsync(_sourceDescriptor, bypassCache: false, cancellation);

            var projectionResults = _projector.ExecuteProjectionAsync(_sourceDescriptor.SourceType, source, scopedServiceProvider, cancellation);

            var metadata = await _targetProcessor.GetMetadataAsync(cancellation);
            var appliedTargets = metadata.Targets.ToHashSet();

            var targets = new List<ProjectionTargetDescriptor>();

            // TODO: Ensure that there are no two projection results with the same type and id. 
            //       Otherwise bad things happen.

            await foreach (var projectionResult in projectionResults)
            {
                var projection = projectionResult.ToTargetDescriptor();
                targets.Add(projection);

                // The target was not part of the last projection. Store ourself to the target metadata.
                var addEntityToProjections = !appliedTargets.Remove(projection);
                await _targetProcessor.UpdateEntityToProjectionAsync(projectionResult, addEntityToProjections, cancellation);
            }

            // We removed all targets from 'applied projections' that are still present. 
            // The remaining ones are removed targets.
            foreach (var removedProjection in appliedTargets)
            {
                await _targetProcessor.RemoveEntityFromProjectionAsync(removedProjection, cancellation);
            }

            var dependencies = GetDependencies(projectionSourceLoader);

            var updatedMetadata = new SourceMetadata(dependencies, targets, sourceRevision);
            await _targetProcessor.UpdateAsync(updatedMetadata, cancellation);
        }

        // Checks whether the projection is up-to date our if we have to reproject.
        // We have to project if
        // - the version of our entity is greater than the projection version
        // - the version of any of our dependencies is greater than the projection version
        private async ValueTask<bool> CheckUpdateNeededAsync(
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            // We load all dependencies from the entity store. 
            // As a projection source's dependencies do not change often, chances are good that the 
            // entities are cached when accessed during the projection phase.
            // For that reason, performance should not suffer very much 
            // in comparison to not checking whether an update is needed.

            var metadata = await _targetProcessor.GetMetadataAsync(cancellation);

            if (metadata.ProjectionRevision == 0)
            {
                return true;
            }

            return await CheckUpdateNeededAsync(metadata, projectionSourceLoader, cancellation);
        }

        private async ValueTask<bool> CheckUpdateNeededAsync(
            SourceMetadata metadata,
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            var sourceRevision = await GetSourceRevisionAsync(_sourceDescriptor, metadata.ProjectionRevision, projectionSourceLoader, cancellation);
            Debug.Assert(sourceRevision >= metadata.ProjectionRevision);

            if (sourceRevision > metadata.ProjectionRevision)
            {
                return true;
            }

            foreach (var (dependency, dependencyProjectionRevision) in metadata.Dependencies)
            {
                var dependencyRevision = await GetSourceRevisionAsync(_sourceDescriptor, metadata.ProjectionRevision, projectionSourceLoader, cancellation);
                Debug.Assert(dependencyRevision >= dependencyProjectionRevision);

                if (dependencyRevision > dependencyProjectionRevision)
                {
                    return true;
                }
            }

            return false;
        }

        private async ValueTask<long> GetSourceRevisionAsync(
            ProjectionSourceDescriptor projectionSource,
            long projectionRevision,
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            long sourceRevision;
            var sourceOutOfDate = false;

            do
            {
                sourceRevision = await projectionSourceLoader.GetSourceRevisionAsync(
                    projectionSource, bypassCache: sourceOutOfDate, cancellation);

                sourceOutOfDate = sourceRevision < projectionRevision;

            } while (sourceRevision < projectionRevision);

            return sourceRevision;
        }

        // Gets our dependencies from the entity store.
        private IEnumerable<ProjectionSourceDependency> GetDependencies(IProjectionSourceLoader projectionSourceLoader)
        {
            var sourceDescriptor = _sourceDescriptor;
            return projectionSourceLoader.LoadedSources.Where(p => p.Dependency != sourceDescriptor).ToArray();
        }
    }
}
