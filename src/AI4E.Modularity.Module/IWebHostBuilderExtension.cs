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
using System.Net.Sockets;
using System.Reflection;
using AI4E.Modularity.Debugging;
using AI4E.Modularity.RPC;
using JsonRpc.DynamicProxy.Client;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Streams;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity
{
    public static class IWebHostBuilderExtension
    {
        private static readonly IJsonRpcContractResolver myContractResolver = new JsonRpcContractResolver
        {
            // Use camelcase for RPC method names.
            NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
            // Use camelcase for the property names in parameter value objects
            ParameterValueConverter = new CamelCaseJsonValueConverter()
        };

        public static IWebHostBuilder UseModuleServer(this IWebHostBuilder webHostBuilder)
        {
            if (webHostBuilder == null)
                throw new ArgumentNullException(nameof(webHostBuilder));

            webHostBuilder.ConfigureServices(services =>
            {
                services.AddOptions();
                services.AddSingleton<IServer>(provider => new ModuleServer(provider.GetRequiredService<IRemoteMessageDispatcher>(), "prefix"));

                //var partManager = services.GetApplicationPartManager();

                // Add the current assembly as application part. The message brokers have to be found.
                //partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
                //services.TryAddSingleton(partManager);

                services.AddSingleton<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
                services.AddSingleton<IEndPointRouter, EndPointRouter>();
                services.AddSingleton<IPhysicalEndPoint<IPEndPoint>, TcpEndPoint>();
                services.AddSingleton<IEndPointManager, EndPointManager<IPEndPoint>>();
                services.AddMessaging();

                services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();
                services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
                services.AddSingleton<IMessageTypeConversion, TypeSerializer>();

                services.AddSingleton(EndPointRoute.CreateRoute("module-TestModule")); // TODO: Get the actual module name
            });

            return webHostBuilder;
        }

        public static IWebHostBuilder UseDebugModuleServer(this IWebHostBuilder webHostBuilder, string moduleName, string prefix)
        {
            if (webHostBuilder == null)
                throw new ArgumentNullException(nameof(webHostBuilder));

            webHostBuilder.ConfigureServices(services =>
            {
                services.AddOptions();
                services.AddSingleton<IServer>(provider => new ModuleServer(provider.GetRequiredService<IRemoteMessageDispatcher>(), prefix));

                //var partManager = services.GetApplicationPartManager();

                // Add the current assembly as application part. The message brokers have to be found.
                //partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
                //services.TryAddSingleton(partManager);

                AI4E.ServiceCollectionExtension.ConfigureApplicationParts(services);


                services.AddSingleton(provider =>
                {
                    var tcpClient = new TcpClient();
                    tcpClient.Connect(IPAddress.Loopback, 8080); // TODO
                    var stream = tcpClient.GetStream();

                    return new RPCHost(stream, provider);
                });

                services.AddSingleton<IRouteStore>(provider =>
                {
                    var rpcHost = provider.GetRequiredService<RPCHost>();
                    var proxy = rpcHost.ActivateAsync<IRouteStore>(ActivationMode.LoadFromServices, cancellation: default).GetAwaiter().GetResult();

                    return new DebugRouteStore(proxy);
                });

                services.AddSingleton<IEndPointManager>(provider =>
                {
                    var rpcHost = provider.GetRequiredService<RPCHost>();
                    var proxy = rpcHost.ActivateAsync<IEndPointManager>(ActivationMode.LoadFromServices, cancellation: default).GetAwaiter().GetResult();

                    return new DebugEndPointManager(proxy);
                });

                services.AddSingleton<IRemoteMessageDispatcher, RemoteMessageDispatcher>(provider => AI4E.ServiceCollectionExtension.BuildMessageDispatcher(provider, new RemoteMessageDispatcher(provider.GetRequiredService<IEndPointRouter>(), provider.GetRequiredService<IMessageTypeConversion>(), provider)) as RemoteMessageDispatcher); // TODO: Bug
                services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<IRemoteMessageDispatcher>());
                services.AddSingleton<IEndPointRouter, EndPointRouter>();  
                services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();
                services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
                services.AddSingleton<IMessageTypeConversion, TypeSerializer>();
                services.AddSingleton(EndPointRoute.CreateRoute($"module-{moduleName}")); // TODO: Get the actual module name
            });

            return webHostBuilder;
        }

        public static IWebHostBuilder UseModuleServer(this IWebHostBuilder webHostBuilder, Action<ModuleServerOptions> configuration)
        {
            if (webHostBuilder == null)
                throw new ArgumentNullException(nameof(webHostBuilder));

            webHostBuilder.UseModuleServer();

            webHostBuilder.ConfigureServices(services =>
            {
                services.Configure(configuration);
            });

            return webHostBuilder;
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
    }

    public class ModuleServerOptions
    {

    }
}
