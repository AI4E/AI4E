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
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Storage
{
    /// <summary>
    /// Contains extensions for the <see cref="IStorageBuilder"/> type.
    /// </summary>
    public static class ProjectionStorageBuilderExtension
    {
        /// <summary>
        /// Adds the projection engine to the storage system.
        /// </summary>
        /// <param name="storageBuilder">The storage builder.</param>
        /// <returns>The storage builder.</returns>
        public static IStorageBuilder AddProjection(this IStorageBuilder storageBuilder)
        {
            AddProjectionCore(storageBuilder);
            return storageBuilder;
        }

        /// <summary>
        /// Adds the projection engine to the storage system.
        /// </summary>
        /// <param name="storageBuilder">The storage builder.</param>
        /// <param name="configuration">An action that configures the projection engine.</param>
        /// <returns>The storage builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        public static IStorageBuilder AddProjection(
            this IStorageBuilder storageBuilder,
            Action<IProjectionBuilder> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            var projectionBuilder = AddProjectionCore(storageBuilder);
            configuration(projectionBuilder);

            return storageBuilder;
        }

        private static IProjectionBuilder AddProjectionCore(this IStorageBuilder storageBuilder)
        {
            var services = storageBuilder.Services;

            services.TryAddSingleton<IProjectionRegistry, ProjectionRegistry>();
            services.TryAddSingleton<IProjectionEngine, ProjectionEngine>();
            services.TryAddSingleton<IProjectionTargetProcessorFactory, ProjectionTargetProcessorFactory>();
            services.TryAddSingleton<IProjectionExecutor, ProjectionExecutor>();
            // TODO: Register a default instance for ProjectionSourceProcessor
            services.ConfigureApplicationParts(ProjectionFeatureProvider.Configure);

            var projectionBuilder = new ProjectionBuilder(storageBuilder);

            Projections.Register(projectionBuilder);

            return projectionBuilder;
        }

        private sealed class ProjectionBuilder : IProjectionBuilder
        {
            private readonly IStorageBuilder _storageBuilder;

            public ProjectionBuilder(IStorageBuilder storageBuilder)
            {
                _storageBuilder = storageBuilder;
            }

            public IProjectionBuilder ConfigureServices(Action<IServiceCollection> configuration)
            {
                configuration(_storageBuilder.Services);

                return this;
            }
        }
    }
}
