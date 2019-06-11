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
    public static class ProjectionStorageBuilderExtension
    {
        public static IStorageBuilder AddProjection(this IStorageBuilder storageBuilder)
        {
            AddProjectionCore(storageBuilder);
            return storageBuilder;
        }

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

        public static IProjectionBuilder AddProjectionCore(this IStorageBuilder storageBuilder)
        {
            var services = storageBuilder.Services;

            services.TryAddSingleton<IProjectionRegistry, ProjectionRegistry>();
            services.TryAddSingleton<IProjectionEngine, ProjectionEngine>();
            services.TryAddSingleton<IProjectionTargetProcessor, ProjectionTargetProcessor>();
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
