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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    public sealed class EntityStorageEngineProjectionSourceProcessor : IProjectionSourceProcessor
    {
        private readonly IEntityStorageEngine _storageEngine;

        public EntityStorageEngineProjectionSourceProcessor(
            IEntityStorageEngine storageEngine, 
            ProjectionSourceDescriptor projectedSource)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            _storageEngine = storageEngine;
            ProjectedSource = projectedSource;
        }

        public ProjectionSourceDescriptor ProjectedSource { get; }

        public IEnumerable<ProjectionSourceDependency> Dependencies => _storageEngine
            .LoadedEntries
            .Select(p => new ProjectionSourceDependency(p.type, p.id, p.revision))
            .Where(p => p.Dependency != ProjectedSource)
            .ToImmutableList();

        public ValueTask<object> GetSourceAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation)
        {
            return _storageEngine.GetByIdAsync(
                projectionSource.SourceType, projectionSource.SourceId, bypassCache, cancellation);
        }

        public ValueTask<long> GetSourceRevisionAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation)
        {
            return _storageEngine.GetRevisionAsync(
                projectionSource.SourceType, projectionSource.SourceId, bypassCache, cancellation);
        }
    }

    public sealed class EntityStorageEngineProjectionSourceProcessorFactory : IProjectionSourceProcessorFactory
    {
        public IProjectionSourceProcessor CreateInstance(
            ProjectionSourceDescriptor projectedSource, 
            IServiceProvider serviceProvider)
        {
            var storageEngine = serviceProvider.GetRequiredService<IEntityStorageEngine>();
            return new EntityStorageEngineProjectionSourceProcessor(storageEngine, projectedSource);
        }
    }
}
