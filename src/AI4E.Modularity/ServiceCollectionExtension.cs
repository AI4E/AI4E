using System;
using System.Reflection;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity
{
    public static class ServiceCollectionExtension
    {
        public static void AddRemoteMessaging(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
            services.AddSingleton<IMessageTypeConversion, TypeSerializer>();
        }

        public static void AddRemoteMessaging(this IServiceCollection services, Action<RemoteMessagingOptions> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            services.AddRemoteMessaging();

            services.Configure(configuration);
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
