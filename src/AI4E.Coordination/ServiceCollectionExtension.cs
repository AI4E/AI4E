using System;
using AI4E.Internal;
using AI4E.Remoting;
using AI4E.Storage;
using AI4E.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public static class ServiceCollectionExtension
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
            services.AddSingleton(p => ConfigureSessionProvider(p, addressType));

            // Add default storage
            services.UseCoordinationStorage<CoordinationStorage>();

            // Add state managers
            services.AddSingleton<IStoredEntryManager, StoredEntryManager>();
            services.AddSingleton<IStoredSessionManager, StoredSessionManager>();

            // Add session manager
            services.AddSingleton<ISessionManager, SessionManager>();

            // Add coordination service
            services.AddSingleton(p => p.GetRequiredService<ICoordinationManagerFactory>().CreateCoordinationManager());
            services.AddSingleton(p => ConfigureCoordinationManagerFactory(p, addressType));
            services.AddScoped(p => ConfigureCoordinationExchangeManager(p, addressType));
            services.AddScoped<ICoordinationWaitManager, CoordinationWaitManager>();
            services.AddScoped<ICoordinationLockManager, CoordinationLockManager>();
            services.AddScoped<CoordinationEntryCache>();
            services.AddScoped<CoordinationSessionManagement>();

            return new CoordinationBuilder(services);
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

        internal static void UseCoordinationStorage<TCoordinationStorage>(this IServiceCollection services)
              where TCoordinationStorage : class, ICoordinationStorage, ISessionStorage
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<TCoordinationStorage>();
            services.AddSingleton<ICoordinationStorage>(p => p.GetRequiredService<TCoordinationStorage>());
            services.AddSingleton<ISessionStorage>(p => p.GetRequiredService<TCoordinationStorage>());
        }

        internal static void UseCoordinationStorage<TCoordinationStorage>(this IServiceCollection services, Func<IServiceProvider, TCoordinationStorage> factory)
              where TCoordinationStorage : class, ICoordinationStorage, ISessionStorage
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton(factory);
            services.AddSingleton<ICoordinationStorage>(p => p.GetRequiredService<TCoordinationStorage>());
            services.AddSingleton<ISessionStorage>(p => p.GetRequiredService<TCoordinationStorage>());
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

    public static class CoordinationBuilderExtension
    {
        public static ICoordinationBuilder UseCoordinationStorage<TCoordinationStorage>(this ICoordinationBuilder builder)
            where TCoordinationStorage : class, ICoordinationStorage, ISessionStorage
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.UseCoordinationStorage<TCoordinationStorage>();

            return builder;
        }

        public static ICoordinationBuilder UseDefaultCoordinationStorage(this ICoordinationBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.UseCoordinationStorage<CoordinationStorage>();

            return builder;
        }

        public static ICoordinationBuilder UseDatabase(this ICoordinationBuilder builder, IFilterableDatabase database)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (database == null)
                throw new ArgumentNullException(nameof(database));

            builder.Services.UseCoordinationStorage(p => BuildCoordinationStorage(p, database));

            return builder;
        }

        public static ICoordinationBuilder UseInMemoryStorage(this ICoordinationBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.Services.UseCoordinationStorage(BuildInMemoryCoordinationStorage);

            return builder;
        }

        private static CoordinationStorage BuildCoordinationStorage(IServiceProvider serviceProvider, IFilterableDatabase database)
        {
            Assert(serviceProvider != null);
            Assert(database != null);

            var storedSessionManager = serviceProvider.GetRequiredService<IStoredSessionManager>();
            var storedEntryManager = serviceProvider.GetRequiredService<IStoredEntryManager>();

            return new CoordinationStorage(database, storedSessionManager, storedEntryManager);
        }

        private static CoordinationStorage BuildInMemoryCoordinationStorage(IServiceProvider serviceProvider)
        {
            return BuildCoordinationStorage(serviceProvider, new InMemoryDatabase());
        }
    }
}
