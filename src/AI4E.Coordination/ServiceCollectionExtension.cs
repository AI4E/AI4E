using System;
using AI4E.Coordination.Caching;
using AI4E.Coordination.Locking;
using AI4E.Coordination.Session;
using AI4E.Coordination.Storage;
using AI4E.Internal;
using AI4E.Remoting;
using AI4E.Storage;
using AI4E.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public static partial class ServiceCollectionExtension
    {
        public static ICoordinationBuilder AddCoordinationService<TAddress>(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return services.AddCoordinationService(typeof(TAddress));
        }

        public static ICoordinationBuilder AddCoordinationService(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return services.AddCoordinationService(null);
        }

        private static ICoordinationBuilder AddCoordinationService(this IServiceCollection services, Type addressType)
        {
            services.AddOptions();

            // Add helpers
            services.AddCoreServices();
            services.TryAddTransient(typeof(IProvider<>), typeof(Provider<>));
            services.AddSingleton(p => ConfigureSessionProvider(p, addressType));

            // Add default storage
            services.AddSingleton<ICoordinationStorage, CoordinationStorage>();
            services.AddSingleton<ISessionStorage, SessionStorage>();

            // Add state managers
            services.AddSingleton<IStoredSessionManager, StoredSessionManager>();

            // Add session manager
            services.AddSingleton<ISessionManager, SessionManager>();

            // Add coordination service
            services.AddSingleton(p => p.GetRequiredService<ICoordinationManagerFactory>().CreateCoordinationManager());
            services.AddSingleton(p => ConfigureCoordinationManagerFactory(p, addressType));
            services.AddScoped(p => ConfigureCoordinationExchangeManager(p, addressType));
            services.AddScoped(typeof(ICoordinationWaitManager), typeof(CoordinationWaitManager));
            services.AddScoped(typeof(ICoordinationLockManager), typeof(CoordinationLockManager));
            services.AddScoped<ICoordinationSessionOwner, CoordinationSessionOwner>();
            services.AddScoped(typeof(ILockWaitDirectory), typeof(LockWaitDirectory));
            services.AddScoped(typeof(ICoordinationCacheManager), typeof(CoodinationCacheManager));
            services.AddScoped(typeof(IInvalidationCallbackDirectory), typeof(InvalidationCallbackDirectory));

            return new CoordinationBuilder(services);
        }

        [Obsolete]
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

        private static ICoordinationExchangeManager ConfigureCoordinationExchangeManager(IServiceProvider serviceProvider, Type addressType)
        {
            if (addressType == null)
                addressType = LookupAddressType(serviceProvider);

            return (ICoordinationExchangeManager)ActivatorUtilities.CreateInstance(serviceProvider, typeof(CoordinationExchangeManager<>).MakeGenericType(addressType));
        }

        private static ICoordinationManagerFactory ConfigureCoordinationManagerFactory(IServiceProvider serviceProvider, Type addressType)
        {
            if (addressType == null)
                addressType = LookupAddressType(serviceProvider);

            return (ICoordinationManagerFactory)ActivatorUtilities.CreateInstance(serviceProvider, typeof(CoordinationManagerFactory<>).MakeGenericType(addressType));
        }

        private static ISessionProvider ConfigureSessionProvider(IServiceProvider serviceProvider, Type addressType)
        {
            if (addressType == null)
                addressType = LookupAddressType(serviceProvider);

            return (ISessionProvider)ActivatorUtilities.CreateInstance(serviceProvider, typeof(SessionProvider<>).MakeGenericType(addressType));
        }

        private static Type LookupAddressType(IServiceProvider serviceProvider)
        {
            var physicalEndPointMarkerService = serviceProvider.GetRequiredService<PhysicalEndPointMarkerService>();
            return physicalEndPointMarkerService.AddressType;
        }

        private sealed class CoordinationBuilder : ICoordinationBuilder
        {
            public CoordinationBuilder(IServiceCollection services)
            {
                if (services == null)
                    throw new ArgumentNullException(nameof(services));

                Services = services;
            }

            public IServiceCollection Services { get; }
        }
    }

    public interface ICoordinationBuilder
    {
        IServiceCollection Services { get; }
    }
}
