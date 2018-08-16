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
                services.AddModuleServices();

                services.AddSingleton<IServer, ModuleServer>();
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
    }
}
