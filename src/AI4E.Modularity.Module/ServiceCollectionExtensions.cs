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
using System.Net;
using System.Reflection;
using AI4E.Utils.ApplicationParts;
using AI4E.Coordination;
using AI4E.Modularity.Debug;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Module
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddModuleServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddOptions();
            services.AddTcpEndPoint();
            services.AddEndPointManager();
            services.AddMessageRouter();
            services.AddRemoteMessageDispatcher();
            services.AddSingleton(ConfigureLogicalEndPoint);
            services.AddSingleton(ConfigureCoordinationManager);
            services.AddSingleton(ConfigureDebugConnection);
            services.AddSingleton<IMetadataAccessor, MetadataAccessor>();
            services.AddSingleton<IModuleManager, ModuleManager>();
            services.AddSingleton<IMetadataReader, MetadataReader>();

            services.AddSingleton<HostProcessMonitor>();
            services.ConfigureApplicationParts(ConfigureApplicationParts);
            services.ConfigureApplicationServices(ConfigureApplicationServices);

            services.AddSingleton<IRouteManagerFactory, RouteManagerFactory>();
            services.AddSingleton(typeof(IEndPointMap<>), typeof(EndPointMap<>));
            services.AddCoordinationService();

            return services;
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<DebugConnection>(isRequiredService: false);
            serviceManager.AddService<HostProcessMonitor>(isRequiredService: true);
        }

        private static ICoordinationManager ConfigureCoordinationManager(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModuleDebugOptions();

            if (options.UseDebugConnection)
            {
                return ActivatorUtilities.CreateInstance<DebugCoordinationManager>(serviceProvider);
            }
            else
            {
                // TODO: Why is this restricted to IPEndPoints?
                return ActivatorUtilities.CreateInstance<CoordinationManager<IPEndPoint>>(serviceProvider);
            }
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            Assert(serviceProvider != null);

            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModuleDebugOptions();
            var remoteOptionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var remoteOptions = remoteOptionsAccessor.Value ?? new RemoteMessagingOptions();

            if (remoteOptions.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            if (options.UseDebugConnection)
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
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleDebugOptions>>();
            var options = optionsAccessor.Value ?? new ModuleDebugOptions();

            if (!options.UseDebugConnection)
            {
                return null;
            }

            return ActivatorUtilities.CreateInstance<DebugConnection>(serviceProvider, optionsAccessor);
        }
    }

    internal static class ServiceCollectionHelper
    {
        private static IServiceCollection AddSingletonConditional<TService, TInstance, TOptions>(this IServiceCollection services, Func<TOptions, bool> condition)
           where TService : class
           where TInstance : class, TService
           where TOptions : class, new()
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            services.AddSingleton<TService, TInstance>(serviceProvider =>
            {
                var optionsAccessor = serviceProvider.GetService<IOptions<TOptions>>();
                var options = optionsAccessor?.Value ?? new TOptions();

                if (!condition(options))
                {
                    return null;
                }

                return ActivatorUtilities.CreateInstance<TInstance>(serviceProvider);
            });

            return services;
        }
    }
}
