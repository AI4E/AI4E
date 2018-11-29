using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.Blazor.Components;
using AI4E.Blazor.Modularity;
using AI4E.Internal;
using AI4E.Modularity.Debug;
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

        private static Task InitializeInstallationSetManagerAsync(IInstallationSetManager installationSetManager, IServiceProvider serviceProvider)
        {
            // TODO: https://github.com/AI4E/AI4E/issues/39
            //       There seems to be a dead-lock with the initialization of the messaging infrastructure if this is not off-loaded.
            Task.Run(async () =>
            {
                var messageDispatcher = serviceProvider.GetRequiredService<IMessageDispatcher>();

                Console.WriteLine("Performing query for running debug modules.");
                var queryResult = await messageDispatcher.QueryAsync<IEnumerable<DebugModule>>(cancellation: default);

                if (!queryResult.IsSuccessWithResult<IEnumerable<DebugModule>>(out var debugModules))
                {
                    throw new Exception("Unable to query installation set."); // TODO: Exception type
                }

                foreach (var debugModule in debugModules)
                {
                    await installationSetManager.InstallAsync(debugModule.Module, cancellation: default);
                }
            });

            return Task.CompletedTask;


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
