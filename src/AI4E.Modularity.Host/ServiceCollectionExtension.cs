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
using System.Linq;
using System.Net;
using System.Reflection;
using AI4E.Coordination;
using AI4E.Modularity.Debugging;
using AI4E.Modularity.HttpDispatch;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Modularity
{
    public static class ServiceCollectionExtension
    {
        public static IModularityBuilder AddModularity(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddOptions();

            services.AddSingleton<IMetadataReader, MetadataReader>();

            // Configure necessary application parts
            var partManager = services.GetApplicationPartManager();
            services.TryAddSingleton(partManager);

            // This is added for the implementation to know that the required services were registered properly.
            services.AddSingleton<ModularityMarkerService>();

            // These services are running only once and therefore are registered as singleton instances.
            // The services are not intended to be used directly but are required for internal use.

            services.TryAddSingleton<IModuleInstaller, ModuleInstaller>();
            services.TryAddSingleton<IModuleSupervision, ModuleSupervision>();

            // These services are the public api for the modular host.
            services.TryAddScoped<IModuleManager, ModuleManager>();

            // High level messaging
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<IRemoteMessageDispatcher>());
            services.AddSingleton<IMessageTypeConversion, TypeSerializer>();

            // Physical messaging
            services.AddSingleton<IPhysicalEndPoint<IPEndPoint>, TcpEndPoint>();
            services.AddSingleton<IEndPointMultiplexer<IPEndPoint>, EndPointMultiplexer<IPEndPoint>>();
            services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();

            // Logical messaging
            services.AddSingleton<IEndPointManager, EndPointManager<IPEndPoint>>();
            services.AddSingleton<IMessageCoder<IPEndPoint>, MessageCoder<IPEndPoint>>();
            services.AddSingleton<ILocalEndPointFactory<IPEndPoint>, LocalEndPointFactory<IPEndPoint>>();
            services.AddSingleton<IEndPointScheduler<IPEndPoint>, RandomEndPointScheduler<IPEndPoint>>();
            services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
            services.AddSingleton<IRouteStore, RouteManager>();
            services.AddSingleton<IRouteMap<IPEndPoint>, RouteMap<IPEndPoint>>();

            // Coordination
            services.AddSingleton<IProvider<ICoordinationManager>, CoordinationManager<IPEndPoint>.Provider>();
            services.AddSingleton(p => p.GetRequiredService<IProvider<ICoordinationManager>>().ProvideInstance());
            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<ISessionProvider, SessionProvider<IPEndPoint>>();
            services.AddSingleton<IStoredEntryManager, StoredEntryManager>();
            services.AddSingleton<IStoredSessionManager, StoredSessionManager>();

            // Utils
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

            // Http-dispatch
            //services.AddSingleton<HttpDispatchTable>();
            services.AddSingleton<IHttpDispatchStore, HttpDispatchStore>();

            // Debugging
            services.AddSingleton<DebugPort>();

            services.Configure<RemoteMessagingOptions>(options =>
            {
                options.LocalEndPoint = EndPointRoute.CreateRoute("host");
            });

            return new ModularityBuilder(services);
        }

        private static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            var manager = services.GetService<ApplicationPartManager>();
            if (manager == null)
            {
                manager = new ApplicationPartManager();
                var parts = DefaultAssemblyPartDiscoveryProvider.DiscoverAssemblyParts(Assembly.GetEntryAssembly().FullName);
                foreach (var part in parts)
                {
                    manager.ApplicationParts.Add(part);
                }
            }

            return manager;
        }

        private static T GetService<T>(this IServiceCollection services)
        {
            return (T)services
                .LastOrDefault(d => d.ServiceType == typeof(T))
                ?.ImplementationInstance;
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
