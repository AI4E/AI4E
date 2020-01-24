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
using AI4E.Modularity.Debug;
using AI4E.Modularity.Host.Debug;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI4E.Modularity.Host
{
    public static class DebugModularityBuilderExtensions
    {
        public static IModularityBuilder UseDebugging(this IModularityBuilder builder, Action<ModularityDebugOptions> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            builder.UseDebugging();
            builder.Services.Configure(configuration);

            return builder;
        }

        public static IModularityBuilder UseDebugging(this IModularityBuilder builder)
        {
            var services = builder.Services;

            services.Configure<ModularityDebugOptions>(options => options.EnableDebugging = true);
            services.AddSingleton(ConfigureDebugPort);
            services.ConfigureApplicationServices(ConfigureApplicationServices);
            services.ConfigureApplicationParts(ConfigureApplicationParts);

            return builder;
        }

        private static DebugPort ConfigureDebugPort(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModularityDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModularityDebugOptions();

            if (options.EnableDebugging)
            {
                return ActivatorUtilities.CreateInstance<DebugPort>(serviceProvider, optionsAccessor);
            }

            return null;
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<DebugPort>(isRequiredService: false);
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(typeof(DebugPort).Assembly));
        }
    }
}
