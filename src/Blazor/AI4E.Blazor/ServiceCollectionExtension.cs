using System;
using System.Reflection;
using AI4E.ApplicationParts;
using AI4E.Blazor.Components;
using AI4E.Blazor.Modularity;
using AI4E.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorModularity(this IServiceCollection services, string entryAssemblyName)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<IModulePrefixLookup, RemoteModulePrefixLookup>();
            services.AddSingleton<IInstallationSetManager, InstallationSetManager>();
            services.AddSingleton<ViewExtensionRenderer>();

            services.ConfigureApplicationParts(ConfigureApplicationParts);
            //services.ConfigureApplicationServices(ConfigureApplicationServices);


        }

        //private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        //{
        //    serviceManager.AddService<IInstallationSetManager>(InitializeInstallationSetManagerAsync);
        //}

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

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.FeatureProviders.Add(new ComponentFeatureProvider());
            partManager.FeatureProviders.Add(new ViewExtensionFeatureProvider());
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
        }
    }
}
