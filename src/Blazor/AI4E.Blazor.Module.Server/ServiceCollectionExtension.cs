using System;
using System.Reflection;
using AI4E.ApplicationParts;
using AI4E.Blazor.Server;
using AI4E.Internal;
using AI4E.Modularity.Module;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor.Module.Server
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorModuleServer(this IServiceCollection services, Assembly appAssembly)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.ConfigureApplicationParts(ConfigureApplicationParts);
            services.AddSingleton<IBlazorModuleManifestProvider, BlazorModuleManifestProvider>(
                p => new BlazorModuleManifestProvider(appAssembly, p.GetRequiredService<IMetadataAccessor>()));
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
        }
    }
}
