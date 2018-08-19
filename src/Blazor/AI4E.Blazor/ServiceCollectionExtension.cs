using System;
using System.Linq;
using AI4E.Blazor.ApplicationParts;
using AI4E.Blazor.Components;
using AI4E.Blazor.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Blazor
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorModularity(this IServiceCollection services, string entryAssemblyName)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<IModulePrefixLookup, RemoteModulePrefixLookup>();

            var partManager = GetApplicationPartManager(services, entryAssemblyName);
            services.TryAddSingleton(partManager);

            partManager.FeatureProviders.Add(new ComponentFeatureProvider());
            partManager.FeatureProviders.Add(new ViewExtensionFeatureProvider());
        }

        private static ApplicationPartManager GetApplicationPartManager(IServiceCollection services, string entryAssemblyName)
        {
            var manager = GetServiceFromCollection<ApplicationPartManager>(services);
            if (manager == null)
            {
                manager = new ApplicationPartManager();

                if (string.IsNullOrEmpty(entryAssemblyName))
                {
                    return manager;
                }

                manager.PopulateDefaultParts(entryAssemblyName);
            }

            return manager;
        }

        private static T GetServiceFromCollection<T>(IServiceCollection services)
        {
            return (T)services
                .LastOrDefault(d => d.ServiceType == typeof(T))
                ?.ImplementationInstance;
        }
    }
}
