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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

#if NETSTD20
using AI4E.Utils;
#endif

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection engine.
    /// </summary>
    public sealed partial class ProjectionEngine : IProjectionEngine
    {
        private readonly IProjectionExecutor _projector;
        private readonly IProjectionSourceProcessorFactory _sourceProcessorFactory;
        private readonly IProjectionTargetProcessorFactory _targetProcessorFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionEngine> _logger;

        /// <summary>
        /// Creates a new instance of type <see cref="ProjectionEngine"/>.
        /// </summary>
        /// <param name="projector">The <see cref="IProjectionExecutor"/> used to execute projections.</param>
        /// <param name="sourceProcessorFactory">The projection source processor.</param>
        /// <param name="targetProcessorFactory">The projection target processor.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <param name="logger">A logger used to log messages or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="projector"/>, <paramref name="sourceProcessorFactory"/>,
        /// <paramref name="targetProcessorFactory"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        public ProjectionEngine(
            IProjectionExecutor projector,
            IProjectionSourceProcessorFactory sourceProcessorFactory,
            IProjectionTargetProcessorFactory targetProcessorFactory,
            IServiceProvider serviceProvider,
            ILogger<ProjectionEngine> logger = default)
        {
            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (sourceProcessorFactory is null)
                throw new ArgumentNullException(nameof(sourceProcessorFactory));

            if (targetProcessorFactory is null)
                throw new ArgumentNullException(nameof(targetProcessorFactory));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _projector = projector;
            _sourceProcessorFactory = sourceProcessorFactory;
            _targetProcessorFactory = targetProcessorFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public Task ProjectAsync(Type entityType, string id, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            var processedEntities = new HashSet<EntityIdentifier>();
            return ProjectAsync(new EntityIdentifier(entityType, id), processedEntities, cancellation);
        }

        private async Task ProjectAsync(EntityIdentifier entity,
                                        ISet<EntityIdentifier> processedEntities,
                                        CancellationToken cancellation)
        {
            if (processedEntities.Contains(entity))
            {
                return;
            }

            var targetProcessor = _targetProcessorFactory.CreateInstance(entity, _serviceProvider);
            var dependents = await ProjectAsync(targetProcessor, cancellation);

            processedEntities.Add(entity);

            foreach (var dependent in dependents)
            {
                await ProjectAsync(dependent, processedEntities, cancellation);
            }
        }

        private async Task<IEnumerable<EntityIdentifier>> ProjectAsync(
            IProjectionTargetProcessor targetProcessor,
            CancellationToken cancellation)
        {
            IEnumerable<EntityIdentifier> dependents;

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
            var sourceProcessor = _sourceProcessorFactory.CreateInstance(
                targetProcessor.ProjectedEntity, scope.ServiceProvider);
            var updateNeeded = await CheckUpdateNeededAsync(sourceProcessor, targetProcessor, cancellation);

            if (!updateNeeded)
                return;

            var entity = await sourceProcessor.GetSourceAsync(
                targetProcessor.ProjectedEntity, cancellation);
            var entityRevision = await sourceProcessor.GetSourceRevisionAsync(
                targetProcessor.ProjectedEntity, expectedMinRevision: default, cancellation);

            var projectionResults = _projector.ExecuteProjectionAsync(
                targetProcessor.ProjectedEntity.EntityType, entity, scopedServiceProvider, cancellation);

            var metadata = await targetProcessor.GetMetadataAsync(cancellation);
            var appliedTargets = metadata.Targets.ToHashSet();

            var targets = new List<ProjectionTargetDescriptor>();

            // Ensure that there are no two projection results with the same type and id. 
            async IAsyncEnumerable<IProjectionResult> ProjectionResultsWithoutDuplicates()
            {
                var processedResults = new HashSet<IProjectionResult>(ProjectionResultComparer.Instance);

                await foreach (var projectionResult in projectionResults)
                {
                    if (!processedResults.Add(projectionResult))
                    {
                        _logger.LogWarning(
                            $"Duplicate projection target with type {projectionResult.ResultType}" +
                            $" and id {projectionResult.ResultId} while projecting entity" +
                            $" '{sourceProcessor.ProjectedSource}'.");

                        continue;
                    }

                    yield return projectionResult;
                }
            }

            await foreach (var projectionResult in ProjectionResultsWithoutDuplicates())
            {
                var projection = projectionResult.AsTargetDescriptor();
                targets.Add(projection);
                await targetProcessor.UpdateTargetAsync(projectionResult, cancellation);

                appliedTargets.Remove(projection);
            }

            // We removed all targets from 'applied projections' that are still present. 
            // The remaining ones are removed targets.
            foreach (var removedProjection in appliedTargets)
            {
                await targetProcessor.RemoveTargetAsync(removedProjection, cancellation);
            }

            var updatedMetadata = new ProjectionMetadata(sourceProcessor.Dependencies, targets, entityRevision);
            await targetProcessor.UpdateAsync(updatedMetadata, cancellation);
        }

        // Checks whether the projection is up-to date or if we have to reproject.
        // We have to project if
        // - the version of our entity is greater than the projection version
        // - the version of any of our dependencies is greater than the projection version
        private async ValueTask<bool> CheckUpdateNeededAsync(
            IProjectionSourceProcessor sourceProcessor,
            IProjectionTargetProcessor targetProcessor,
            CancellationToken cancellation)
        {
            // We load all dependencies from the entity store. 
            // As an entity's dependencies do not change often, chances are good that the 
            // entities are cached when accessed during the projection phase.
            // For that reason, performance should not suffer very much 
            // in comparison to not checking whether an update is needed.

            var metadata = await targetProcessor.GetMetadataAsync(cancellation);

            if (metadata.ProjectionRevision == 0)
            {
                return true;
            }

            return await CheckUpdateNeededAsync(sourceProcessor, metadata, cancellation);
        }

        private async ValueTask<bool> CheckUpdateNeededAsync(
            IProjectionSourceProcessor sourceProcessor,
            ProjectionMetadata metadata,
            CancellationToken cancellation)
        {
            var entityRevision = await sourceProcessor.GetSourceRevisionAsync(
                   sourceProcessor.ProjectedSource,
                   expectedMinRevision: metadata.ProjectionRevision,
                   cancellation);

            // The entity is not existent, ie. it was deleted.
            if (entityRevision == 0)
            {
                return true;
            }

            Debug.Assert(entityRevision >= metadata.ProjectionRevision);

            if (entityRevision > metadata.ProjectionRevision)
            {
                return true;
            }

            foreach (var projectionRevision in metadata.Dependencies.Select(p => p.ProjectionRevision))
            {
                var dependencyRevision = await sourceProcessor.GetSourceRevisionAsync(
                   sourceProcessor.ProjectedSource,
                   expectedMinRevision: metadata.ProjectionRevision,
                   cancellation);

                // The dependency is not existent, ie. it was deleted.
                if (dependencyRevision == 0)
                {
                    return true;
                }

                Debug.Assert(dependencyRevision >= projectionRevision);

                if (dependencyRevision > projectionRevision)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
