using System;
using System.Reflection;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI4E.Routing
{
    public static class EndPointManagerServiceCollectionExtension
    {
        //public static void AddEndPointManager<TAddress>(this IServiceCollection services)
        //{
        //    services.ConfigureEndPointManager(typeof(TAddress), () => services.AddSingleton<IEndPointManager<TAddress>, EndPointManager<TAddress>>());
        //}

        public static void AddEndPointManager(this IServiceCollection services)
        {
            services.AddSingleton(typeof(IEndPointManager<>), typeof(EndPointManager<>));
            services.AddSingleton(ConfigureEndPointManager);
            services.AddSingleton(ConfigureLogicalEndPoint);
            services.AddHelperServices();
        }

        private static IEndPointManager ConfigureEndPointManager(IServiceProvider provider)
        {
            var physicalEndPointMarkerService = provider.GetRequiredService<PhysicalEndPointMarkerService>();
            var addressType = physicalEndPointMarkerService.AddressType;

            return (IEndPointManager)provider.GetRequiredService(typeof(IEndPointManager<>).MakeGenericType(addressType));
        }

        //public static void AddEndPointManager<TAddress, TEndPointManager>(this IServiceCollection services)
        //    where TEndPointManager : class, IEndPointManager<TAddress>
        //{
        //    if (services == null)
        //        throw new ArgumentNullException(nameof(services));

        //    services.ConfigureEndPointManager(typeof(TAddress), () => services.AddSingleton<IEndPointManager<TAddress>, TEndPointManager>());
        //}

        //public static void AddEndPointManager<TAddress, TEndPointManager>(this IServiceCollection services, TEndPointManager endPointManager)
        //    where TEndPointManager : class, IEndPointManager<TAddress>
        //{
        //    if (services == null)
        //        throw new ArgumentNullException(nameof(services));

        //    if (endPointManager == null)
        //        throw new ArgumentNullException(nameof(endPointManager));

        //    services.ConfigureEndPointManager(typeof(TAddress), () => services.AddSingleton<IEndPointManager<TAddress>>(endPointManager));
        //}

        //public static void AddEndPointManager<TAddress, TEndPointManager>(this IServiceCollection services, Func<IServiceProvider, TEndPointManager> factory)
        //    where TEndPointManager : class, IEndPointManager<TAddress>
        //{
        //    if (services == null)
        //        throw new ArgumentNullException(nameof(services));

        //    if (factory == null)
        //        throw new ArgumentNullException(nameof(factory));

        //    services.ConfigureEndPointManager(typeof(TAddress), () => services.AddSingleton<IEndPointManager<TAddress>, TEndPointManager>(factory));
        //}

        //private static void ConfigureEndPointManager(this IServiceCollection services, Type addressType, Action configuration)
        //{
        //    services.AddSingleton(provider => (IEndPointManager)provider.GetRequiredService(typeof(IEndPointManager).MakeGenericType(addressType)));
        //    services.AddSingleton(ConfigureLogicalEndPoint);
        //    services.AddHelperServices();
        //}

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var options = optionsAccessor.Value ?? new RemoteMessagingOptions();

            if (options.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            return endPointManager.GetLogicalEndPoint(options.LocalEndPoint);
        }

        private static void AddHelperServices(this IServiceCollection services)
        {
            services.AddOptions();
            services.AddCoreServices();

            services.AddSingleton(typeof(IMessageCoder<>), typeof(MessageCoder<>));
            services.AddSingleton(typeof(IEndPointScheduler<>), typeof(RandomEndPointScheduler<>));
            services.AddSingleton(typeof(IRouteMap<>), typeof(RouteMap<>));
            services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();

            services.AddCoordinationService();
        }

        public static void AddRemoteMessageDispatcher(this IServiceCollection services)
        {
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<IMessageTypeConversion, TypeSerializer>();
            services.AddSingleton<IRouteStore, RouteManager>();
            services.AddEndPointManager();
        }
    }

    public sealed class RemoteMessagingOptions
    {
        public RemoteMessagingOptions()
        {
            LocalEndPoint = EndPointRoute.CreateRoute(Assembly.GetEntryAssembly().GetName().Name);
        }

        public EndPointRoute LocalEndPoint { get; set; }
    }
}
