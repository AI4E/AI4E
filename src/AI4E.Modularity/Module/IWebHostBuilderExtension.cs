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
using System.Net.Sockets;
using AI4E.Coordination;
using AI4E.Modularity.Debug;
using AI4E.Proxying;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Module
{
    public static class IWebHostBuilderExtension
    {
        public static IWebHostBuilder UseModuleServer(this IWebHostBuilder webHostBuilder)
        {
            if (webHostBuilder == null)
                throw new ArgumentNullException(nameof(webHostBuilder));

            webHostBuilder.ConfigureServices(services =>
            {
                services.AddOptions();
                services.AddSingleton<IServer, ModuleServer>();
                services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
                services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();
                services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
                services.AddSingleton<IMessageTypeConversion, TypeSerializer>();
                services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
                services.AddSingleton<IRouteStore, RouteManager>();
                services.AddSingleton<IRouteMap<IPEndPoint>, RouteMap<IPEndPoint>>();

                // Http-dispatch
                services.AddSingleton<IHttpDispatchStore, HttpDispatchStore>();

                services.AddSingleton(ConfigurePhysicalEndPoint);
                services.AddSingleton(ConfigureProxyHost);
                services.AddSingleton(ConfigureEndPointManager);
                services.AddSingleton(ConfigureCoordinationManager);
                services.AddSingleton(ConfigureLogicalEndPoint);
            });

            return webHostBuilder;
        }

        public static IWebHostBuilder UseModuleServer(this IWebHostBuilder webHostBuilder, Action<ModuleServerOptions> configuration)
        {
            if (webHostBuilder == null)
                throw new ArgumentNullException(nameof(webHostBuilder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = webHostBuilder.UseModuleServer();

            webHostBuilder.ConfigureServices(services => services.Configure(configuration));

            return result;
        }

        private static IPhysicalEndPoint<IPEndPoint> ConfigurePhysicalEndPoint(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (options.UseDebugConnection)
            {
                return null;
            }

            return ActivatorUtilities.CreateInstance<UdpEndPoint>(serviceProvider);
        }

        private static ICoordinationManager ConfigureCoordinationManager(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (options.UseDebugConnection)
            {
                return ActivatorUtilities.CreateInstance<DebugCoordinationManager>(serviceProvider);
            }
            else
            {
                return ActivatorUtilities.CreateInstance<CoordinationManager<IPEndPoint>>(serviceProvider);
            }
        }

        private static IEndPointManager ConfigureEndPointManager(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (options.UseDebugConnection)
            {
                return null;
            }
            else
            {
                return ActivatorUtilities.CreateInstance<EndPointManager<IPEndPoint>>(serviceProvider);
            }
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            Assert(serviceProvider != null);

            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();
            var remoteOptionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var remoteOptions = remoteOptionsAccessor.Value ?? new RemoteMessagingOptions();

            if (remoteOptions.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            if (options.UseDebugConnection)
            {
                var proxyHost = serviceProvider.GetRequiredService<ProxyHost>();
                return new DebugLogicalEndPoint(proxyHost, remoteOptions.LocalEndPoint);
            }
            else
            {
                var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
                return endPointManager.GetLogicalEndPoint(remoteOptions.LocalEndPoint);
            }
        }

        private static ProxyHost ConfigureProxyHost(IServiceProvider serviceProvider)
        {
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (!options.UseDebugConnection)
            {
                return null;
            }

            var addressSerializer = serviceProvider.GetRequiredService<IAddressConversion<IPEndPoint>>();
            var endPoint = addressSerializer.Parse(options.DebugConnection);
            var tcpClient = new TcpClient(endPoint.AddressFamily);
            tcpClient.Connect(endPoint.Address, endPoint.Port);
            var stream = tcpClient.GetStream();
            return new ProxyHost(stream, serviceProvider);
        }
    }
}
