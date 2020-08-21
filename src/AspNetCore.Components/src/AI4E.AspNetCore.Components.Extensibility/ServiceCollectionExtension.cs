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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AI4E;
using AI4E.AspNetCore.Components;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.AspNetCore.Components.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AI4EComponentsServiceCollectionExtension
    {
        private static IBlazorModularityBuilder AddBlazorModuleManagerCore(
            this IServiceCollection services,
            Assembly? entryAssembly)
        {
            var descriptor = services.FirstOrDefault(p => p.ServiceType == typeof(BlazorModularityBuilder));

            if (descriptor != null)
            {
                Debug.Assert(descriptor.ImplementationInstance != null);

                return (IBlazorModularityBuilder)descriptor.ImplementationInstance!;
            }

            var builder = new BlazorModularityBuilder(services)
                .UseBlazorCachingWorkaround()
                .UseAutofacCachingWorkaround();

            services.AddSingleton(builder);

            services.TryAddSingleton<IBlazorModuleManager, BlazorModuleManager>();
            services.TryAddSingleton<IBlazorModuleSourceFactory>(NoModuleSourceFactory.Instance);
            services.AddAssemblyRegistry(registry =>
            {
                if (entryAssembly != null)
                {
                    var assemblies = ComponentResolver.EnumerateComponentAssemblies(entryAssembly);
                    registry.AddAssemblies(assemblies);
                }
            });

            BlazorModuleRunner.Configure(services);
            return builder;
        }

        public static IBlazorModularityBuilder AddBlazorModularity(
            this IServiceCollection services,
            Assembly entryAssembly)
        {
            if (entryAssembly is null)
                throw new ArgumentNullException(nameof(entryAssembly));

            return AddBlazorModuleManagerCore(services, entryAssembly);
        }

        public static IBlazorModularityBuilder AddBlazorModularity(this IServiceCollection services)
        {
            return AddBlazorModuleManagerCore(services, entryAssembly: null);
        }

        public static IBlazorModularityBuilder AddBlazorModularity(
            this IServiceCollection services,
            Action<BlazorModuleOptions> configuration)
        {
            return AddBlazorModuleManagerCore(services, entryAssembly: null).Configure(configuration);
        }

        public static IBlazorModularityBuilder AddBlazorModularity(
            this IServiceCollection services,
            Assembly entryAssembly,
            Action<BlazorModuleOptions> configuration)
        {
            if (entryAssembly is null)
                throw new ArgumentNullException(nameof(entryAssembly));

            return AddBlazorModuleManagerCore(services, entryAssembly).Configure(configuration);
        }
    }
}