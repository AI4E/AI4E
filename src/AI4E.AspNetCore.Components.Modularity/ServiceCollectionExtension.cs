using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Blazor.Components;
using AI4E.Blazor.Modularity;
using AI4E.Modularity.Debug;
using AI4E.Modularity.Host;
using AI4E.Routing.SignalR.Client;
using AI4E.Utils.ApplicationParts;
using BlazorSignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace AI4E.Blazor
{
    public static class ServiceCollectionExtension
    {
        internal static readonly string _defaultHubUrl = "/MessageDispatcherHub"; // TODO: This should be configured only once.

        public static void AddBlazorMessageDispatcher(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            void ConfigureHubConnection(
                IHubConnectionBuilder hubConnectionBuilder,
                IServiceProvider serviceProvider)
            {
                var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
                hubConnectionBuilder.WithUrlBlazor(new Uri(_defaultHubUrl, UriKind.Relative), jsRuntime);
            }

            services.AddSignalRMessageDispatcher(ConfigureHubConnection);
        }

        public static void AddBlazorModularity(this IServiceCollection services, Assembly entryAssembly)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddBlazorMessageDispatcher();
            services.AddSingleton<IModulePropertiesLookup, RemoteModulePropertiesLookup>();
            services.AddSingleton<IModuleManifestProvider, ModuleManifestProvider>();
            services.AddSingleton<IModuleAssemblyDownloader, ModuleAssemblyDownloader>();
            services.AddSingleton<IInstallationSetManager, InstallationSetManager>();
            services.AddSingleton<ViewExtensionRenderer>();

            services.ConfigureApplicationParts(partManager => ConfigureApplicationParts(partManager, entryAssembly));
            services.ConfigureApplicationServices(ConfigureApplicationServices);
        }

        public static void AddBlazorModularity(this IServiceCollection services)
        {
            AddBlazorModularity(services, Assembly.GetCallingAssembly());
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IMessageDispatcher>();
            serviceManager.AddService<IInstallationSetManager>(InitializeInstallationSetManagerAsync);
        }

        private static async Task InitializeInstallationSetManagerAsync(IInstallationSetManager installationSetManager, IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetService<ILogger<object>>(); // TODO
            var messageDispatcher = serviceProvider.GetRequiredService<IMessageDispatcher>();

            logger?.LogDebug("Performing query for running debug modules.");
            var queryResult = await messageDispatcher.QueryAsync<IEnumerable<DebugModuleProperties>>(cancellation: default);

            if (!queryResult.IsSuccessWithResult<IEnumerable<DebugModuleProperties>>(out var debugModules))
            {
                throw new Exception("Unable to query installation set."); // TODO: Exception type
            }

            foreach (var debugModule in debugModules)
            {
                await installationSetManager.InstallAsync(debugModule.Module, cancellation: default);
            }

            //var queryResult = await messageDispatcher.QueryAsync<ResolvedInstallationSet>(cancellation: default);

            //if (!queryResult.IsSuccessWithResult<ResolvedInstallationSet>(out var installationSet))
            //{
            //    throw new Exception("Unable to query installation set."); // TODO: Exception type
            //}

            //await installationSetManager.UpdateInstallationSetAsync(installationSet.Resolved.Select(p => p.Module), cancellation: default);
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager, Assembly entryAssembly)
        {
            partManager.FeatureProviders.Add(new ComponentFeatureProvider());
            partManager.FeatureProviders.Add(new ViewExtensionFeatureProvider());
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
            partManager.ApplicationParts.Add(new AssemblyPart(entryAssembly));
        }
    }
}
