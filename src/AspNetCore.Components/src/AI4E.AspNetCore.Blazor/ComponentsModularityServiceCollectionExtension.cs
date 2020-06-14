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
using System.Reflection;
using AI4E;
using AI4E.AspNetCore.Blazor;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Messaging;
using AI4E.Messaging.SignalR.Client;
using AI4E.Modularity;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils.DependencyInjection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ComponentsModularityServiceCollectionExtension
    {
        internal const string DefaultHubUrl = "/MessageDispatcherHub"; // TODO: This should be configured only once.

        public static void AddBlazorMessageDispatcher(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            static void ConfigureHubConnection(
                IHubConnectionBuilder hubConnectionBuilder,
                IServiceProvider serviceProvider)
            {
                var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
                var navigationManager = serviceProvider.GetRequiredService<NavigationManager>();
                hubConnectionBuilder.WithUrlBlazor(new Uri(DefaultHubUrl, UriKind.Relative), jsRuntime, navigationManager);
            }

            services.AddSignalRMessageDispatcher(ConfigureHubConnection);
        }

        public static void AddBlazorModularity(this IServiceCollection services, Assembly entryAssembly)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton(new AssemblyManager(entryAssembly));
            services.AddSingleton<IAssemblySource>(p => p.GetRequiredService<AssemblyManager>());

            services.AddBlazorMessageDispatcher();

            services.AddSingleton<IRunningModuleManager, RemoteRunningModuleManager>();
            services.AddSingleton<IModulePropertiesLookup, RemoteModulePropertiesLookup>();
            services.AddSingleton<IModuleManifestProvider, ModuleManifestProvider>();
            services.AddSingleton<IModuleAssemblyDownloader, ModuleAssemblyDownloader>();
            services.AddSingleton<IInstallationSetManager, InstallationSetManager>();

            services.ConfigureApplicationParts(partManager => ConfigureApplicationParts(partManager, entryAssembly));
            services.ConfigureApplicationServices(ConfigureApplicationServices);

            services.AddSingleton(ServerSideIndicator.Instance);
        }

        public static void AddBlazorModularity(this IServiceCollection services)
        {
            AddBlazorModularity(services, Assembly.GetCallingAssembly());
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IMessageDispatcher>();
            serviceManager.AddService<IInstallationSetManager>();
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager, Assembly entryAssembly)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
            partManager.ApplicationParts.Add(new AssemblyPart(entryAssembly));
        }
    }
}
