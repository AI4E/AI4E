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
using AI4E.Proxying;
using AI4E.Routing;
using AI4E.Routing.Debugging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI4E.Modularity
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
                services.AddSingleton(ConfigureRPCHost);
                services.AddSingleton(ConfigureEndPointManager);
                services.AddSingleton(ConfigureRouteStore);
                services.AddSingleton(ConfigurePhysicalEndPoint);
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

        private static IPhysicalEndPoint<IPEndPoint> ConfigurePhysicalEndPoint(IServiceProvider provider)
        {
            var optionsAccessor = provider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (options.UseDebugConnection)
            {
                return null;
            }

            return ActivatorUtilities.CreateInstance<TcpEndPoint>(provider);
        }

        private static IRouteStore ConfigureRouteStore(IServiceProvider provider)
        {
            var optionsAccessor = provider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (!options.UseDebugConnection)
            {
                return null;
            }

            return ActivatorUtilities.CreateInstance<DebugRouteStore>(provider);

        }

        private static IEndPointManager ConfigureEndPointManager(IServiceProvider provider)
        {
            var optionsAccessor = provider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (options.UseDebugConnection)
            {
                return ActivatorUtilities.CreateInstance<DebugEndPointManager>(provider);
            }
            else
            {
                return null; /* TODO */
                //return ActivatorUtilities.CreateInstance<EndPointManager<IPEndPoint>>(provider);
            }
        }

        private static ProxyHost ConfigureRPCHost(IServiceProvider provider)
        {
            var optionsAccessor = provider.GetRequiredService<IOptions<ModuleServerOptions>>();
            var options = optionsAccessor.Value ?? new ModuleServerOptions();

            if (!options.UseDebugConnection)
            {
                return null;
            }

            var addressSerializer = provider.GetRequiredService<IAddressConversion<IPEndPoint>>();
            var endPoint = addressSerializer.Parse(options.DebugConnection);
            var tcpClient = new TcpClient(endPoint.AddressFamily);
            tcpClient.Connect(endPoint.Address, endPoint.Port);
            var stream = tcpClient.GetStream();
            return new ProxyHost(stream, provider);
        }
    }
}
