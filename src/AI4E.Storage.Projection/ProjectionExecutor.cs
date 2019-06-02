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
using AI4E.Internal;
using AI4E.Utils;

namespace AI4E.Storage.Projection
{
    public sealed class ProjectionExecutor : IProjectionExecutor
    {
        private readonly IProjectionRegistry _projectionRegistry;
        private volatile IProjectionProvider _projectionProvider;

        public ProjectionExecutor(IProjectionRegistry projectionRegistry)
        {
            if (projectionRegistry is null)
                throw new ArgumentNullException(nameof(projectionRegistry));

            _projectionRegistry = projectionRegistry;
        }

        // TODO: Do we allow projections reload?
        private void ReloadProjections()
        {
            _projectionProvider = null; // Volatile write op.
        }

        /// <summary>
        /// Gets the <see cref="IProjectionProvider"/> that is used to load projections.
        /// </summary>
        public IProjectionProvider ProjectionProvider => GetProjectionProvider();

        private IProjectionProvider GetProjectionProvider()
        {
            var projectionProvider = _projectionProvider; // Volatile read op.

            if (projectionProvider == null)
            {
                projectionProvider = _projectionRegistry.ToProvider();
                var previous = Interlocked.CompareExchange(ref _projectionProvider, projectionProvider, null);

                if (previous != null)
                {
                    projectionProvider = previous;
                }
            }

            Debug.Assert(projectionProvider != null);

            return projectionProvider;
        }

        public IAsyncEnumerable<IProjectionResult> ExecuteProjectionAsync(
            Type sourceType,
            object source, // May be null
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            if (source is null)
            {
                return AsyncEnumerable.Empty<IProjectionResult>();
            }

            if (sourceType.IsValueType || sourceType.IsDelegate())
                throw new ArgumentException("The argument must be a reference type.", nameof(sourceType));

            if (!sourceType.IsAssignableFrom(source.GetType()))
                throw new ArgumentException($"The argument '{nameof(source)}' must be of the type specified by '{nameof(sourceType)}' or a derived type.");

            return ExecuteProjectionInternalAsync(sourceType, source, serviceProvider, cancellation);
        }

        private static IEnumerable<Type> GetProjectionTypeHierarchy(Type sourceType)
        {
            var currType = sourceType;

            do
            {
                yield return currType;
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);
        }

        private IAsyncEnumerable<IProjectionResult> ExecuteProjectionInternalAsync(
            Type sourceType,
            object source,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            var projectionProvider = GetProjectionProvider();

            // There is no parallelism (with Task.WhenAll(projectors.Select(...)) used because we cannot guarantee that it is allowed to access 'source' concurrently.
            // But it is possible to change the return type to IAsyncEnumerable<IProjectionResult> and process each batch on access. 
            // This allows to remove the up-front evaluation and storage of the results.

            return GetProjectionTypeHierarchy(sourceType)
                .SelectMany(type => projectionProvider.GetProjectionRegistrations(type)).ToAsyncEnumerable()
                .SelectMany(provider => ExecuteSingleProjectionAsync(provider, sourceType, source, serviceProvider, cancellation));
        }

        private IAsyncEnumerable<IProjectionResult> ExecuteSingleProjectionAsync(
            IProjectionRegistration projectionRegistration,
            Type sourceType,
            object source,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            var projection = projectionRegistration.CreateProjection(serviceProvider);

            if (projection == null)
            {
                throw new InvalidOperationException($"Cannot execute a projection for source type '{projection.SourceType}' that is null.");
            }

            if (!projection.SourceType.IsAssignableFrom(sourceType))
            {
                throw new InvalidOperationException($"Cannot execute a projection for source type '{projection.SourceType}' with a source of type '{sourceType}'.");
            }

            return projection.ProjectAsync(source, cancellation)
                                    .Where(p => !(p is null))
                                    .Select(p => new ProjectionResult(projection.TargetType, p));
        }
    }
}
