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
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Projection
{
    public sealed partial class ProjectionEngine : IProjectionEngine
    {
        private readonly IProjectionExecutor _projector;
        private readonly IDatabase _database;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionEngine> _logger;

        public ProjectionEngine(IProjectionExecutor projector,
                                IDatabase database,
                                IServiceProvider serviceProvider,
                                ILogger<ProjectionEngine> logger = default)
        {
            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _projector = projector;
            _database = database;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task ProjectAsync(Type entityType, string id, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            var processedSources = new HashSet<ProjectionSourceDescriptor>();
            return ProjectAsync(new ProjectionSourceDescriptor(entityType, id), processedSources, cancellation);
        }

        private async Task ProjectAsync(ProjectionSourceDescriptor source,
                                        ISet<ProjectionSourceDescriptor> processedSources,
                                        CancellationToken cancellation)
        {
            if (processedSources.Contains(source))
            {
                return;
            }

            var targetProcessor = new ProjectionTargetProcessor(source, _database); // TODO: Inject an instance of IProjectionTargetProcessorFactory
            var dependents = await ProjectAsync(targetProcessor, cancellation);

            processedSources.Add(source);

            foreach (var dependent in dependents)
            {
                await ProjectAsync(dependent, processedSources, cancellation);
            }
        }

        private async Task<IEnumerable<ProjectionSourceDescriptor>> ProjectAsync(IProjectionTargetProcessor targetProcessor, CancellationToken cancellation)
        {
            IEnumerable<ProjectionSourceDescriptor> dependents;

            do
            {
                await ProjectCoreAsync(targetProcessor, cancellation);
                dependents = await targetProcessor.GetDependentsAsync(cancellation);
            }
            while (!await targetProcessor.CommitAsync(cancellation));

            return dependents;
        }

        private async Task ProjectCoreAsync(IProjectionTargetProcessor targetProcessor, CancellationToken cancellation)
        {
            targetProcessor.Clear();

            using var scope = _serviceProvider.CreateScope();
            var scopedServiceProvider = scope.ServiceProvider;
            var projectionSourceLoader = scopedServiceProvider.GetRequiredService<IProjectionSourceLoader>();
            var updateNeeded = await CheckUpdateNeededAsync(projectionSourceLoader, targetProcessor, cancellation);

            if (!updateNeeded)
                return;

            var source = await projectionSourceLoader.GetSourceAsync(targetProcessor.ProjectedSource, bypassCache: false, cancellation);
            var sourceRevision = await projectionSourceLoader.GetSourceRevisionAsync(targetProcessor.ProjectedSource, bypassCache: false, cancellation);

            var projectionResults = _projector.ExecuteProjectionAsync(targetProcessor.ProjectedSource.SourceType, source, scopedServiceProvider, cancellation);

            var metadata = await targetProcessor.GetMetadataAsync(cancellation);
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
                await targetProcessor.UpdateEntityToProjectionAsync(projectionResult, addEntityToProjections, cancellation);
            }

            // We removed all targets from 'applied projections' that are still present. 
            // The remaining ones are removed targets.
            foreach (var removedProjection in appliedTargets)
            {
                await targetProcessor.RemoveEntityFromProjectionAsync(removedProjection, cancellation);
            }

            var dependencies = GetDependencies(projectionSourceLoader, targetProcessor.ProjectedSource);

            var updatedMetadata = new SourceMetadata(dependencies, targets, sourceRevision);
            await targetProcessor.UpdateAsync(updatedMetadata, cancellation);
        }

        // Checks whether the projection is up-to date our if we have to reproject.
        // We have to project if
        // - the version of our entity is greater than the projection version
        // - the version of any of our dependencies is greater than the projection version
        private async ValueTask<bool> CheckUpdateNeededAsync(
            IProjectionSourceLoader projectionSourceLoader,
            IProjectionTargetProcessor targetProcessor,
            CancellationToken cancellation)
        {
            // We load all dependencies from the entity store. 
            // As a projection source's dependencies do not change often, chances are good that the 
            // entities are cached when accessed during the projection phase.
            // For that reason, performance should not suffer very much 
            // in comparison to not checking whether an update is needed.

            var metadata = await targetProcessor.GetMetadataAsync(cancellation);

            if (metadata.ProjectionRevision == 0)
            {
                return true;
            }

            return await CheckUpdateNeededAsync(targetProcessor.ProjectedSource, metadata, projectionSourceLoader, cancellation);
        }

        private async ValueTask<bool> CheckUpdateNeededAsync(
            ProjectionSourceDescriptor projectionSource,
            SourceMetadata metadata,
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            var sourceRevision = await GetSourceRevisionAsync(projectionSource, metadata.ProjectionRevision, projectionSourceLoader, cancellation);
            Debug.Assert(sourceRevision >= metadata.ProjectionRevision);

            if (sourceRevision > metadata.ProjectionRevision)
            {
                return true;
            }

            foreach (var (dependency, dependencyProjectionRevision) in metadata.Dependencies)
            {
                var dependencyRevision = await GetSourceRevisionAsync(projectionSource, metadata.ProjectionRevision, projectionSourceLoader, cancellation);
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
        private IEnumerable<ProjectionSourceDependency> GetDependencies(IProjectionSourceLoader projectionSourceLoader, ProjectionSourceDescriptor projectionSource)
        {
            return projectionSourceLoader.LoadedSources.Where(p => p.Dependency != projectionSource).ToArray();
        }
    }
}
