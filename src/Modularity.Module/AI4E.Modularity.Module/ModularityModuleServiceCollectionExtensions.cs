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
using System.Reflection;
using AI4E;
using AI4E.Messaging;
using AI4E.Modularity;
using AI4E.Modularity.Metadata;
using AI4E.Modularity.Module;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    internal static class ModularityModuleServiceCollectionExtensions
    {
        public static IServiceCollection AddModuleServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddOptions();
            services.AddMessaging()
                .UseTcpEndPoint();

            services.AddSingleton<IMetadataAccessor, MetadataAccessor>();
            services.AddSingleton<IModuleManager, ModuleManager>();
            services.AddSingleton<IMetadataReader, MetadataReader>();

            services.AddSingleton<HostProcessMonitor>();

            services.ConfigureApplicationServices(ConfigureApplicationServices);
            services.ConfigureApplicationParts(ConfigureApplicationParts);

            return services;
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<HostProcessMonitor>(isRequiredService: true);
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
        }
    }
}
