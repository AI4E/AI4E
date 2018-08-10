using System;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using static AI4E.Internal.MessageDispatcherBuilder;

namespace AI4E.Routing.Blazor
{
    public static class ServiceCollectionExtension
    {
        public static void AddBlazorMessageDispatcher(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddCoreServices();
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<ITypeConversion, TypeSerializer>();

            services.AddSingleton<IClientEndPoint, ClientEndPoint>();
            services.AddSingleton<ILogicalClientEndPoint, LogicalClientEndPoint>();
            services.AddSingleton<IMessageRouterFactory, RemoteMessageRouterFactory>();
        }

        private static void AddMessageDispatcher<TMessageDispatcher, TMessageDispatcherImpl>(this IServiceCollection services)
            where TMessageDispatcher : class, IMessageDispatcher
            where TMessageDispatcherImpl : class, TMessageDispatcher
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.AddSingleton<TMessageDispatcher>(serviceProvider => BuildMessageDispatcher(serviceProvider, ActivatorUtilities.CreateInstance<TMessageDispatcherImpl>(serviceProvider)));
            services.AddSingleton<IMessageDispatcher>(provider => provider.GetRequiredService<TMessageDispatcher>());
        }
    }
}
