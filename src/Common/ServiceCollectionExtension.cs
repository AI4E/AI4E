using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Internal
{
    internal static class ServiceCollectionExtension
    {
        public static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var manager = services.GetService<ApplicationPartManager>();
            if (manager == null)
            {
                manager = new ApplicationPartManager();
                manager.ApplicationParts.Add(new AssemblyPart(Assembly.GetEntryAssembly()));
            }

            return manager;
        }

        public static T GetService<T>(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(T));

            return (T)serviceDescriptor?.ImplementationInstance;
        }

        public static void ConfigureApplicationParts(this IServiceCollection services, Action<ApplicationPartManager> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var partManager = services.GetApplicationPartManager();
            configuration(partManager);
            services.TryAddSingleton(partManager);
        }
    }
}
