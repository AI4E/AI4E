using System;
using System.Reflection;
using AI4E.ApplicationParts;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class ServiceCollectionExtension
    {
        public static IServiceCollection AddCoreServices(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // This must be registered as transient to allow retrieval of scoped services.
            services.TryAddTransient(typeof(IProvider<>), typeof(Provider<>));
            services.TryAddSingleton(typeof(IContextualProvider<>), typeof(ContextualProvider<>));
            services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();

            return services;
        }

        private sealed class Provider<T> : IProvider<T>
        {
            private readonly IServiceProvider _serviceProvider;

            public Provider(IServiceProvider serviceProvider)
            {
                Assert(serviceProvider != null);
                _serviceProvider = serviceProvider;
            }

            public T ProvideInstance()
            {
                return _serviceProvider.GetRequiredService<T>();
            }
        }

        private sealed class ContextualProvider<T> : IContextualProvider<T>
        {
            public ContextualProvider() { }

            public T ProvideInstance(IServiceProvider serviceProvider)
            {
                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                return serviceProvider.GetRequiredService<T>();
            }
        }

        public static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var manager = services.GetService<ApplicationPartManager>();
            if (manager == null)
            {
                manager = new ApplicationPartManager();

                var entryAssembly = Assembly.GetEntryAssembly();

                // Blazor cannot access the entry assembly apparently.
                if (entryAssembly != null)
                {
                    manager.ApplicationParts.Add(new AssemblyPart(entryAssembly));
                }
            }

            return manager;
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
