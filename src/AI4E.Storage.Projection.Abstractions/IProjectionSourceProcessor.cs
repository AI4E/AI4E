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
    public interface IProjectionSourceProcessor
    {
        ProjectionSourceDescriptor ProjectedSource { get; }

        ValueTask<object> GetSourceAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation = default);

        ValueTask<long> GetSourceRevisionAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation = default);

        IEnumerable<ProjectionSourceDependency> Dependencies { get; }
    }

    public interface IProjectionSourceProcessorFactory
    {
        IProjectionSourceProcessor CreateInstance(ProjectionSourceDescriptor projectedSource, IServiceProvider serviceProvider);
    }

    public static class ProjectionSourceProcessorExtension
    {
        public static ValueTask<object> GetSourceAsync(
            this IProjectionSourceProcessor sourceProcessor,
            bool bypassCache,
            CancellationToken cancellation = default)
        {
            return sourceProcessor.GetSourceAsync(sourceProcessor.ProjectedSource, bypassCache, cancellation);
        }

        public static ValueTask<long> GetSourceRevisionAsync(
            this IProjectionSourceProcessor sourceProcessor,
            bool bypassCache,
            CancellationToken cancellation = default)
        {
            return sourceProcessor.GetSourceRevisionAsync(sourceProcessor.ProjectedSource, bypassCache, cancellation);
        }
    }
}
