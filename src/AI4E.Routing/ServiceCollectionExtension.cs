using System;
using System.Reflection;
using AI4E.AspNetCore;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI4E.Routing
{
    public static class ServiceCollectionExtension
    {
        private static IEndPointManager ConfigureEndPointManager(IServiceProvider provider)
        {
            var physicalEndPointMarkerService = provider.GetRequiredService<PhysicalEndPointMarkerService>();
            var addressType = physicalEndPointMarkerService.AddressType;

            return (IEndPointManager)provider.GetRequiredService(typeof(IEndPointManager<>).MakeGenericType(addressType));
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
            var optionsAccessor = serviceProvider.GetRequiredService<IOptions<RemoteMessagingOptions>>();
            var options = optionsAccessor.Value ?? new RemoteMessagingOptions();

            if (options.LocalEndPoint == default)
            {
                throw new InvalidOperationException("A local end point must be specified.");
            }

            return endPointManager.CreateLogicalEndPoint(options.LocalEndPoint);
        }

        private static void AddHelperServices(this IServiceCollection services)
        {
            services.AddOptions();
            services.AddCoreServices();

            services.AddSingleton(typeof(IEndPointScheduler<>), typeof(RandomEndPointScheduler<>));
            services.AddSingleton(typeof(IEndPointMap<>), typeof(EndPointMap<>));

            services.AddCoordinationService();
        }

        public static void AddEndPointManager(this IServiceCollection services)
        {
            services.AddSingleton(typeof(IEndPointManager<>), typeof(EndPointManager<>));
            services.AddSingleton(ConfigureEndPointManager);
            services.AddSingleton(ConfigureLogicalEndPoint);
            services.AddHelperServices();
        }

        public static void AddMessageRouter(this IServiceCollection services)
        {
            services.AddSingleton<IRouteManagerFactory, RouteManagerFactory>();
            services.AddSingleton<IMessageRouterFactory, MessageRouterFactory>();
            services.AddHelperServices();
        }

        public static void AddRemoteMessageDispatcher(this IServiceCollection services)
        {
            services.AddCoreServices();
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<ITypeConversion, TypeSerializer>();
        }
    }

    public sealed class RemoteMessagingOptions
    {
        public RemoteMessagingOptions()
        {
            LocalEndPoint = new EndPointAddress(Assembly.GetEntryAssembly().GetName().Name);
        }

        public EndPointAddress LocalEndPoint { get; set; }
    }
}
