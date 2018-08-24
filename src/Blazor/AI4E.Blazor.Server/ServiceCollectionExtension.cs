using System;
using System.Reflection;
using AI4E.ApplicationParts;
using AI4E.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor.Server
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorServer(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.ConfigureApplicationParts(ConfigureApplicationParts);
        }

        private static void ConfigureApplicationParts(ApplicationParts.ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
        }
    }
}
