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

        public static IServiceCollection ConfigureHubConnectionBuilder(this IServiceCollection services, Action<IHubConnectionBuilder> configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var hubConnectionBuilder = services.GetService<IHubConnectionBuilder>();

            if (hubConnectionBuilder == null)
            {
                hubConnectionBuilder = new HubConnectionBuilder(services);
            }

            configuration(hubConnectionBuilder);
            services.Add(new ServiceDescriptor(typeof(IHubConnectionBuilder), hubConnectionBuilder));

            return services;
        }

        public static IServiceCollection AddSignalRMessageDispatcher(this IServiceCollection serviceCollection)
        {
            if (serviceCollection == null)
                throw new ArgumentNullException(nameof(serviceCollection));

            serviceCollection.AddSignalRMessageDispatcherCore(hubConnectionBuilder => hubConnectionBuilder.WithUrl(_defaultHubUrl));
            return serviceCollection;
        }

        public static IServiceCollection AddSignalRMessageDispatcher(this IServiceCollection serviceCollection, string url)
        {
            if (serviceCollection == null)
                throw new ArgumentNullException(nameof(serviceCollection));

            if (url == null)
                throw new ArgumentNullException(nameof(url));

            serviceCollection.AddSignalRMessageDispatcherCore(hubConnectionBuilder => hubConnectionBuilder.WithUrl(url));
            return serviceCollection;
        }

        public static IServiceCollection AddSignalRMessageDispatcher(
            this IServiceCollection serviceCollection,
            Action<IHubConnectionBuilder> configureHubConnection)
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
            Action<IHubConnectionBuilder> configureHubConnection)
        {
            serviceCollection.AddCoreServices();
            serviceCollection.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            serviceCollection.AddSingleton<ITypeConversion, TypeSerializer>();

            serviceCollection.AddSingleton<IClientEndPoint, ClientEndPoint>();
            serviceCollection.AddSingleton<IRequestReplyClientEndPoint, RequestReplyClientEndPoint>();
            serviceCollection.AddSingleton<IMessageRouterFactory, RemoteMessageRouterFactory>();

            serviceCollection.ConfigureHubConnectionBuilder(configureHubConnection);
            // Do NOT Add the HubConnection to the service container.
            // The connection builder already does this and it would lead to infinite recursion.

            serviceCollection.ConfigureApplicationServices(serviceManager => serviceManager.AddService<IMessageDispatcher>());
        }
    }
}
