using System;
using AI4E.Remoting;
using AI4E.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Routing.SignalR.Client
{
    public static class ServiceCollectionExtension
    {
        internal static readonly string _defaultHubUrl = "/MessageDispatcherHub"; // TODO: This should be configured only once.

        public static IServiceCollection ConfigureHubConnectionBuilder(
            this IServiceCollection services,
            Action<IHubConnectionBuilder, IServiceProvider> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            HubConnection BuildHubConnection(IServiceProvider provider)
            {
                var hubConnectionBuilder = new HubConnectionBuilder();
                configuration(hubConnectionBuilder, provider);
                return hubConnectionBuilder.Build();
            }

            services.AddSingleton(BuildHubConnection);

            return services;
        }

        public static IServiceCollection AddSignalRMessageDispatcher(this IServiceCollection serviceCollection)
        {
            if (serviceCollection == null)
                throw new ArgumentNullException(nameof(serviceCollection));

            serviceCollection.AddSignalRMessageDispatcherCore((hubConnectionBuilder, _) => hubConnectionBuilder.WithUrl(_defaultHubUrl));
            return serviceCollection;
        }

        public static IServiceCollection AddSignalRMessageDispatcher(this IServiceCollection serviceCollection, string url)
        {
            if (serviceCollection == null)
                throw new ArgumentNullException(nameof(serviceCollection));

            if (url == null)
                throw new ArgumentNullException(nameof(url));

            serviceCollection.AddSignalRMessageDispatcherCore((hubConnectionBuilder, _) => hubConnectionBuilder.WithUrl(url));
            return serviceCollection;
        }

        public static IServiceCollection AddSignalRMessageDispatcher(
            this IServiceCollection serviceCollection,
            Action<IHubConnectionBuilder, IServiceProvider> configureHubConnection)
        {
            if (serviceCollection == null)
                throw new ArgumentNullException(nameof(serviceCollection));

            if (configureHubConnection == null)
                throw new ArgumentNullException(nameof(configureHubConnection));

            serviceCollection.AddSignalRMessageDispatcherCore(configureHubConnection);
            return serviceCollection;
        }

        private static void AddSignalRMessageDispatcherCore(
            this IServiceCollection serviceCollection,
            Action<IHubConnectionBuilder, IServiceProvider> configureHubConnection)
        {
            serviceCollection.AddCoreServices();
            serviceCollection.AddSingleton<IRemoteMessageDispatcher>(p => p.GetRequiredService<RemoteMessageDispatcher>());
            serviceCollection.AddMessageDispatcher<RemoteMessageDispatcher>();
            serviceCollection.AddSingleton<ITypeConversion, TypeSerializer>();

            serviceCollection.AddSingleton<IClientEndPoint, ClientEndPoint>();
            serviceCollection.AddSingleton<IRequestReplyClientEndPoint, RequestReplyClientEndPoint>();
            serviceCollection.AddSingleton<IMessageRouterFactory, RemoteMessageRouterFactory>();

            serviceCollection.ConfigureHubConnectionBuilder(configureHubConnection);
        }
    }
}
