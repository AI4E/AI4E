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
using System.Reflection;
using AI4E.ApplicationParts;
using AI4E.ApplicationParts.Utils;
using AI4E.Coordination;
using AI4E.Domain.Services;
using AI4E.Modularity.Debug;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            services.AddModuleManagement();
            services.AddSingleton<ModularityMarkerService>();
            services.AddEndPointManager();
            services.AddMessageRouter();
            services.AddRemoteMessageDispatcher();
            services.AddUdpEndPoint();
            services.AddSingleton(ConfigureDebugPort);
            services.ConfigureApplicationServices(ConfigureApplicationServices);

            services.AddSingleton<IModuleManager, ModuleManager>();
            services.AddSingleton<IPathMapper, PathMapper>();

            services.AddSingleton<IRouteManagerFactory, RouteManagerFactory>();
            services.AddSingleton(typeof(IEndPointMap<>), typeof(EndPointMap<>));
            services.AddCoordinationService();

            return new ModularityBuilder(services);
        }

        private static void AddModuleManagement(this IServiceCollection services)
        {
            services.AddScoped<IModuleSearchEngine, ModuleSearchEngine>();
            services.AddScoped<IDependencyResolver, DependencyResolver>();
            services.AddSingleton<IModuleInstaller, ModuleInstaller>();
            services.AddSingleton<IModuleSupervisorFactory, ModuleSupervisorFactory>();
            services.AddSingleton<IModuleInstallationManager, ModuleInstallationManager>();
            services.AddSingleton<IMetadataReader, MetadataReader>();
            services.AddDomainServices();
            services.ConfigureApplicationParts(ConfigureApplicationParts);
            services.ConfigureApplicationServices(ConfigureApplicationServices);
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IMessageDispatcher>();
            serviceManager.AddService<DebugPort>(isRequiredService: false);
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
            partManager.ApplicationParts.Add(new AssemblyPart(typeof(DebugPort).Assembly));
        }

        private static DebugPort ConfigureDebugPort(IServiceProvider serviceProvider)
        {
            Assert(serviceProvider != null);

            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<ModularityOptions>>();
            var options = optionsAccessor.Value ?? new ModularityOptions();



            if (options.EnableDebugging)
            {
                var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
                return ActivatorUtilities.CreateInstance<DebugPort>(serviceProvider, optionsAccessor);
            }

            return null;
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
