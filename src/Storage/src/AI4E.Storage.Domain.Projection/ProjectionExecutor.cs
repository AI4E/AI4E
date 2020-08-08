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

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection executor that is used to execute single projections.
    /// </summary>
    public sealed class ProjectionExecutor : IProjectionExecutor
    {
        private readonly IProjectionRegistry _projectionRegistry;
        private volatile IProjectionProvider _projectionProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionExecutor"/> type.
        /// </summary>
        /// <param name="projectionRegistry">The projection registry that contains all registered projections.</param>
        public ProjectionExecutor(IProjectionRegistry projectionRegistry)
        {
            if (projectionRegistry is null)
                throw new ArgumentNullException(nameof(projectionRegistry));

            _projectionRegistry = projectionRegistry;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IAsyncEnumerable<IProjectionResult> ExecuteProjectionAsync(
            Type entityType,
            object entity, // May be null
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            // TODO
            if (entityType.IsValueType || typeof(Delegate).IsAssignableFrom(entityType) /*sourceType.IsDelegate()*/)
                throw new ArgumentException("The argument must be a reference type.", nameof(entityType));

            if (entity is null)
                return AsyncEnumerable.Empty<IProjectionResult>();

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException(
                    $"The argument '{nameof(entity)}' must be of the type specified by '{nameof(entityType)}'" +
                    $" or a derived type.");

            return ExecuteProjectionInternalAsync(entityType, entity, serviceProvider, cancellation);
        }

        private static IEnumerable<Type> GetProjectionTypeHierarchy(Type entityType)
        {
            var currType = entityType;

            do
            {
                yield return currType;
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);
        }

        private IAsyncEnumerable<IProjectionResult> ExecuteProjectionInternalAsync(
            Type entityType,
            object entity,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            var projectionProvider = GetProjectionProvider();

            // There is no parallelism (with Task.WhenAll(projectors.Select(...)) used because we cannot guarantee
            // that it is allowed to access 'entity' concurrently. It is possible to use the return type
            // IAsyncEnumerable<IProjectionResult> and process each batch on access. This allows to remove the up-front
            // evaluation and storage of the results.

            return GetProjectionTypeHierarchy(entityType)
                .SelectMany(type => projectionProvider.GetProjectionRegistrations(type)).ToAsyncEnumerable()
                .SelectMany(provider => ExecuteSingleProjectionAsync(
                    provider, entityType, entity, serviceProvider, cancellation));
        }

        private IAsyncEnumerable<IProjectionResult> ExecuteSingleProjectionAsync(
            IProjectionRegistration projectionRegistration,
            Type entityType,
            object entity,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            var projection = projectionRegistration.CreateProjection(serviceProvider);

            if (projection == null)
            {
                throw new InvalidOperationException(
                    $"Cannot execute a projection for entity-type '{projection.EntityType}' that is null.");
            }

            if (!projection.EntityType.IsAssignableFrom(entityType))
            {
                throw new InvalidOperationException(
                    $"Cannot execute a projection for entity-type '{projection.EntityType}'" +
                    $" with a entity of type '{entityType}'.");
            }

            return projection.ProjectAsync(entity, cancellation)
                .Where(p => !(p is null))
                .Select(p => new ProjectionResult(projection.TargetType, p));
        }
    }
}
