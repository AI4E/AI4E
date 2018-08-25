using System;
using System.Reflection;
using AI4E.ApplicationParts;
using AI4E.Blazor.Components;
using AI4E.Blazor.Modularity;
using AI4E.Internal;
using AI4E.Routing.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorModularity(this IServiceCollection services, Assembly entryAssembly)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddBlazorMessageDispatcher();
            services.AddSingleton<IModulePrefixLookup, RemoteModulePrefixLookup>();
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
            //serviceManager.AddService<IInstallationSetManager>(InitializeInstallationSetManagerAsync);
        }

        //private static async Task InitializeInstallationSetManagerAsync(IInstallationSetManager installationSetManager, IServiceProvider serviceProvider)
        //{
        //    var messageDispatcher = serviceProvider.GetRequiredService<IMessageDispatcher>();

        //    var queryResult = await messageDispatcher.QueryAsync<ResolvedInstallationSet>(cancellation: default);

        //    if (!queryResult.IsSuccessWithResult<ResolvedInstallationSet>(out var installationSet))
        //    {
        //        throw new Exception("Unable to query installation set."); // TODO: Exception type
        //    }

        //    await installationSetManager.UpdateInstallationSetAsync(installationSet.Resolved.Select(p => p.Module), cancellation: default);
        //}

        private static void ConfigureApplicationParts(ApplicationPartManager partManager, Assembly entryAssembly)
        {
            partManager.FeatureProviders.Add(new ComponentFeatureProvider());
            partManager.FeatureProviders.Add(new ViewExtensionFeatureProvider());
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
            partManager.ApplicationParts.Add(new AssemblyPart(entryAssembly));
        }
    }
}
