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
using AI4E.Modularity.Module;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Hosting
{
    public static class ModularityModuleWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseModuleServer(this IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureServices(services =>
            {
                services.AddModuleServices();
                services.AddSingleton<IServer, ModuleServer>();
            });

            return webHostBuilder;
        }

        public static IWebHostBuilder UseModuleServer(this IWebHostBuilder webHostBuilder, Action<IModuleBuilder> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = webHostBuilder.UseModuleServer();
            var moduleBuilder = new ModuleBuilder(webHostBuilder);

            configuration(moduleBuilder);

            return result;
        }

        private sealed class ModuleBuilder : IModuleBuilder
        {
            private readonly IWebHostBuilder _webHostBuilder;

            public ModuleBuilder(IWebHostBuilder webHostBuilder)
            {
                _webHostBuilder = webHostBuilder;
            }

            public IModuleBuilder ConfigureServices(Action<IServiceCollection> configureServices)
            {
                _webHostBuilder.ConfigureServices(configureServices);
                return this;
            }
        }
    }
}
