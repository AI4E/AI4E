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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;

namespace AI4E.Storage.Domain.Projection
{
    public sealed class EntityStorageEngineProjectionSourceProcessor : IProjectionSourceProcessor
    {
        private readonly IEntityStorage _entityStorage;

        public EntityStorageEngineProjectionSourceProcessor(
            IEntityStorage entityStorage,
            EntityIdentifier projectedSource)
        {
            if (entityStorage == null)
                throw new ArgumentNullException(nameof(entityStorage));

            _entityStorage = entityStorage;
            ProjectedSource = projectedSource;
        }

        public EntityIdentifier ProjectedSource { get; }

        public IEnumerable<ProjectionSourceDependency> Dependencies => _entityStorage
            .LoadedEntities
            .Select(p => new ProjectionSourceDependency(
                p.EntityIdentifier.EntityType, p.EntityIdentifier.EntityId, p.Revision))
            .Where(p => p.Dependency != ProjectedSource)
            .ToImmutableList();

        public async ValueTask<object?> GetSourceAsync(
            EntityIdentifier projectionSource,
            CancellationToken cancellation)
        {
            var entityLoadResult = await _entityStorage.LoadEntityAsync(
                new EntityIdentifier(
                    projectionSource.EntityType, projectionSource.EntityId), cancellation).ConfigureAwait(false);

            return entityLoadResult?.GetEntity(throwOnFailure: false);
        }

        private static readonly ObjectPool<ExpectedRevisionDomainQueryProcessor> _domainQueryProcessorPool
            = ObjectPool.Create<ExpectedRevisionDomainQueryProcessor>();

        public async ValueTask<long> GetSourceRevisionAsync(
            EntityIdentifier projectionSource,
            long? expectedMinRevision,
            CancellationToken cancellation)
        {
            var entityIdentifier = new EntityIdentifier(projectionSource.EntityType, projectionSource.EntityId);

            using (_domainQueryProcessorPool.Get(out var domainQueryProcessor))
            {
                domainQueryProcessor.MinExpectedRevision = expectedMinRevision;
                domainQueryProcessor.MaxExpectedRevision = null;

                var entityLoadResult = await _entityStorage.LoadEntityAsync(
                    entityIdentifier, domainQueryProcessor, cancellation).ConfigureAwait(false);

                return entityLoadResult?.Revision ?? 0;
            }
        }
    }

    public sealed class EntityStorageEngineProjectionSourceProcessorFactory : IProjectionSourceProcessorFactory
    {
        public IProjectionSourceProcessor CreateInstance(
            EntityIdentifier projectedSource,
            IServiceProvider serviceProvider)
        {
            return ActivatorUtilities.CreateInstance<EntityStorageEngineProjectionSourceProcessor>(
                serviceProvider, projectedSource);
        }
    }
}
