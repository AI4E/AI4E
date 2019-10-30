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
using System.Diagnostics;
using System.Linq;
using AI4E.AspNetCore.Components.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AI4EComponentsServiceCollectionExtension
    {
        public static IBlazorModularityBuilder AddModularity(this IServiceCollection services)
        {
            var descriptor = services.FirstOrDefault(p => p.ServiceType == typeof(BlazorModularityBuilder));

            if (descriptor != null)
            {
                Debug.Assert(descriptor.ImplementationInstance != null);

                return (IBlazorModularityBuilder)descriptor.ImplementationInstance!;
            }

            var builder = new BlazorModularityBuilder(services);
            services.AddSingleton(builder);

            services.TryAddSingleton<IBlazorModuleSource, BlazorModuleSource>();
#if SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT // TODO
            services.TryAddSingleton<IBlazorModuleManager, BlazorModuleManager>();
#endif
            services.AddSingleton<IBlazorModuleAssemblyLoader, BlazorModuleAssemblyLoader>();
            BlazorModuleRunner.Configure(services);
            return builder;
        }

        public static IBlazorModularityBuilder AddModularity(
            this IServiceCollection services,
            Action<BlazorModuleOptions> configuration)
        {
            return AddModularity(services).Configure(configuration);
        }
    }
}