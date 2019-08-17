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
using AI4E.Coordination;
using AI4E.Modularity.Debug;
using AI4E.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.Modularity.Module
{
    public static class DebugModuleBuilderExtensions
    {
        public static IModuleBuilder UseDebugging(this IModuleBuilder builder, Action<ModularityDebugOptions> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            builder.UseDebugging();
            builder.ConfigureServices(services => services.Configure(configuration));

            return builder;
        }

        public static IModuleBuilder UseDebugging(this IModuleBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<ModularityDebugOptions>(options => options.EnableDebugging = true);
                services.AddSingleton(ConfigureLogicalEndPoint);
                services.AddSingleton(ConfigureCoordinationManager);
                services.AddSingleton(ConfigureDebugConnection);
                services.ConfigureApplicationServices(ConfigureApplicationServices);
            });

            return builder;
        }

        private static ICoordinationManager ConfigureCoordinationManager(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModularityDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModularityDebugOptions();

            if (options.EnableDebugging)
            {
                return ActivatorUtilities.CreateInstance<DebugCoordinationManager>(serviceProvider);
            }
            else
            {
                return serviceProvider.GetRequiredService<ICoordinationManagerFactory>().CreateCoordinationManager();
            }
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModularityDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModularityDebugOptions();
            var remoteOptionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var remoteOptions = remoteOptionsAccessor.Value ?? new RemoteMessagingOptions();

            if (remoteOptions.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            if (options.EnableDebugging)
            {
                var debugConnection = serviceProvider.GetRequiredService<DebugConnection>();
                var logger = serviceProvider.GetService<ILogger<DebugLogicalEndPoint>>();
                return new DebugLogicalEndPoint(debugConnection, remoteOptions.LocalEndPoint, logger);
            }
            else
            {
                var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
                return endPointManager.CreateLogicalEndPoint(remoteOptions.LocalEndPoint);
            }
        }

        private static DebugConnection ConfigureDebugConnection(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModularityDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModularityDebugOptions();

            if (!options.EnableDebugging)
            {
                return null;
            }

            return ActivatorUtilities.CreateInstance<DebugConnection>(serviceProvider, optionsAccessor);
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<DebugConnection>(isRequiredService: false);
        }
    }
}
