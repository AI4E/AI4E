using System;
using System.Collections.Generic;
using System.Reflection;
using AI4E.Remoting;
using AI4E.Validation;
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
            services.AddDateTimeProvider();

            services.AddSingleton(typeof(IEndPointScheduler<>), typeof(RandomEndPointScheduler<>));
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
            services.AddSingleton<IMessageRouterFactory, MessageRouterFactory>();
            services.AddHelperServices();
        }

        public static void AddRemoteMessageDispatcher(this IServiceCollection services)
        {
            services.AddDateTimeProvider();
            services.ConfigureApplicationServices(RemoteMessageDispatcherInitialization);

            services.AddSingleton(p => p.GetRequiredService<IMessageDispatcher>() as IRemoteMessageDispatcher);
            services.AddMessaging().UseDispatcher<RemoteMessageDispatcher>();
        }

        private static void RemoteMessageDispatcherInitialization(ApplicationServiceManager serviceManager)
        {
            serviceManager.AddService<IRemoteMessageDispatcher>(isRequiredService: true);
        }
    }

    public sealed class RemoteMessagingOptions
    {
        public RemoteMessagingOptions()
        {
            var assemblyName = Assembly.GetEntryAssembly()?.GetName()?.Name;

            if (assemblyName != null)
            {
                LocalEndPoint = new EndPointAddress(assemblyName);
            }

            RoutesResolvers = new List<IRoutesResolver> { new ValidationRoutesResolver() };
        }

        public EndPointAddress LocalEndPoint { get; set; }
        public IList<IRoutesResolver> RoutesResolvers { get; }
    }

    internal sealed class ValidationRoutesResolver : RoutesResolver
    {
        public override bool CanResolve(DispatchDataDictionary dispatchData)
        {
            return typeof(Validate).IsAssignableFrom(dispatchData.MessageType);
        }

        public override RouteHierarchy Resolve(DispatchDataDictionary dispatchData)
        {
            var underlyingType = (dispatchData.Message as Validate).MessageType;

            return ResolveDefaults(underlyingType);
        }
    }
}
