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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain.Projection
{
    internal static class Projections
    {
        public static void Register(IProjectionBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Protect us from registering the projections multiple times.
                if (services.Any(p => p.ServiceType == typeof(ProjectionRegisteredMarker)))
                    return;

                services.AddSingleton<ProjectionRegisteredMarker>(_ => null);
                builder.ConfigureProjections(Configure);
            });
        }

        private sealed class ProjectionRegisteredMarker { }

        private static void Configure(IProjectionRegistry projectionRegistry, IServiceProvider serviceProvider)
        {
            var assemblyRegistry = serviceProvider.GetRequiredService<IAssemblyRegistry>();
            var projectionResolver = serviceProvider.GetRequiredService<IProjectionResolver>();

            // TODO: Can we update the projection registry when the assembly registry updates?
            var projectionTypes = projectionResolver.ResolveProjections(assemblyRegistry.AssemblySource);

            RegisterProjectionsTypes(projectionTypes, projectionRegistry);
        }

        private static void RegisterProjectionsTypes(
            IEnumerable<Type> types,
            IProjectionRegistry projectionRegistry)
        {
            foreach (var type in types)
            {
                RegisterProjectionsType(type, projectionRegistry);
            }
        }

        private static void RegisterProjectionsType(
            Type type,
            IProjectionRegistry projectionRegistry)
        {
            var projectionDescriptors = ProjectionInspector.Instance.InspectType(type);

            foreach (var projectionDescriptor in projectionDescriptors)
            {
                var registration = CreateProjectionRegistration(projectionDescriptor);
                projectionRegistry.Register(registration);
            }
        }

        private static IProjectionRegistration CreateProjectionRegistration(ProjectionDescriptor projectionDescriptor)
        {
            return new ProjectionRegistration(
                serviceProvider => ProjectionInvoker.CreateInvoker(projectionDescriptor, serviceProvider),
                projectionDescriptor);
        }
    }
}
