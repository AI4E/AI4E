/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EntityStorageEngine.cs 
 * Types:           (1) AI4E.Storage.Domain.EntityStorageEngine
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   23.06.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Projection;

namespace AI4E.Storage.Domain
{
    public sealed class ProjectionSourceLoader : IProjectionSourceLoader
    {
        private readonly IEntityStorageEngine _storageEngine;

        public ProjectionSourceLoader(IEntityStorageEngine storageEngine)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            _storageEngine = storageEngine;
        }

        public IEnumerable<ProjectionSourceDependency> LoadedSources => _storageEngine.LoadedEntries.Select(p => new ProjectionSourceDependency(p.type, p.id, p.revision));

        public ValueTask<object> GetSourceAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation)
        {
            return _storageEngine.GetByIdAsync(projectionSource.SourceType, projectionSource.SourceId, bypassCache, cancellation);
        }

        public ValueTask<long> GetSourceRevisionAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation)
        {
            return _storageEngine.GetRevisionAsync(projectionSource.SourceType, projectionSource.SourceId, bypassCache, cancellation);
        }
    }
}
