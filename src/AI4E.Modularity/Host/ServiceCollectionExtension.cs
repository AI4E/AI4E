/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Coordination;
using AI4E.Domain.Services;
using AI4E.Internal;
using AI4E.Modularity.Debug;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public static class ServiceCollectionExtension
    {
        public static IModularityBuilder AddModularity(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddOptions();

            // Module management
            services.AddModuleManagement();

            // This is added for the implementation to know that the required services were registered properly.
            services.AddSingleton<ModularityMarkerService>();

            // High level messaging
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<IRemoteMessageDispatcher>());
            services.AddSingleton<IMessageTypeConversion, TypeSerializer>();

            // Physical messaging
            services.AddSingleton<IPhysicalEndPoint<IPEndPoint>, UdpEndPoint>();
            services.AddSingleton<IPhysicalEndPointMultiplexer<IPEndPoint>, PhysicalEndPointMultiplexer<IPEndPoint>>();
            services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();

            // Logical messaging
            services.AddSingleton<IEndPointManager, EndPointManager<IPEndPoint>>();
            services.AddSingleton(ConfigureLogicalEndPoint);
            services.AddSingleton<IMessageCoder<IPEndPoint>, MessageCoder<IPEndPoint>>();
            services.AddSingleton<IEndPointScheduler<IPEndPoint>, RandomEndPointScheduler<IPEndPoint>>();
            services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
            services.AddSingleton<IRouteStore, RouteManager>();
            services.AddSingleton<IRouteMap<IPEndPoint>, RouteMap<IPEndPoint>>();

            // Coordination
            services.AddCoordinationService<IPEndPoint>();

            // Utils
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            // Http-dispatch
            services.AddSingleton<IHttpDispatchStore, HttpDispatchStore>();

            // Debugging
            services.AddSingleton(ConfigureDebugPort);

            services.Configure<RemoteMessagingOptions>(options =>
            {
                options.LocalEndPoint = EndPointRoute.CreateRoute("host");
            });

            return new ModularityBuilder(services);
        }

        private static void AddModuleManagement(this IServiceCollection services)
        {
            services.AddScoped<IModuleSearchEngine, ModuleSearchEngine>();
            services.AddScoped<IDependencyResolver, DependencyResolver>();
            services.AddSingleton<IModuleInstaller, ModuleInstaller>();
            services.AddSingleton<IModuleSupervisorFactory, ModuleSupervisorFactory>();
            services.AddSingleton<IModuleManager, ModuleManager>();
            services.AddSingleton<IMetadataReader, MetadataReader>();
            services.AddDomainServices();
            services.ConfigureApplicationParts(partManager => partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly())));
        }

        private static DebugPort ConfigureDebugPort(IServiceProvider serviceProvider)
        {
            Assert(serviceProvider != null);

            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModularityOptions>>();
            var options = optionsAccessor.Value ?? new ModularityOptions();

            if (options.EnableDebugging)
            {
                return new DebugPort(serviceProvider, serviceProvider.GetRequiredService<IAddressConversion<IPEndPoint>>(), optionsAccessor);
            }

            return null;
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            Assert(serviceProvider != null);

            var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var options = optionsAccessor.Value ?? new RemoteMessagingOptions();

            if (options.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            return endPointManager.GetLogicalEndPoint(options.LocalEndPoint);
        }

        public static IModularityBuilder AddModularity(this IServiceCollection services, Action<ModularityOptions> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = services.AddModularity();

            result.Configure(configuration);

            return result;
        }

        private sealed class ModularityBuilder : IModularityBuilder
        {
            public ModularityBuilder(IServiceCollection services)
            {
                Services = services;
            }

            public IServiceCollection Services { get; }
        }
    }

    internal class ModularityMarkerService { }
}
