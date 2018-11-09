using System;
using AI4E.Internal;
using AI4E.Remoting;
using Blazor.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Routing.Blazor
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorMessageDispatcher(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            AddBlazorMessageDispatcher(services, ConfigureHubConnection);
        }

        public static void AddBlazorMessageDispatcher(this IServiceCollection services, Func<IServiceProvider, HubConnection> configureHubConnection)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configureHubConnection == null)
                throw new ArgumentNullException(nameof(configureHubConnection));

            services.AddCoreServices();
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<ITypeConversion, TypeSerializer>();

            services.AddSingleton<IClientEndPoint, ClientEndPoint>();
            services.AddSingleton<IRequestReplyClientEndPoint, RequestReplyClientEndPoint>();
            services.AddSingleton<IMessageRouterFactory, RemoteMessageRouterFactory>();
            services.AddSingleton(configureHubConnection);
        }

        private static HubConnection ConfigureHubConnection(IServiceProvider serviceProvider)
        {
            var connection = new HubConnectionBuilder()
                                .WithUrl("/MessageDispatcherHub", opt =>
                                {
                                    opt.LogLevel = SignalRLogLevel.Trace;
                                    opt.Transport = HttpTransportType.WebSockets;
                                })
                                .Build();

            return connection;
        }
    }
}
