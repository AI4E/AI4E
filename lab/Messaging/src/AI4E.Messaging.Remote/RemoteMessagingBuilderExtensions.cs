using System.Net;
using AI4E.Storage.Coordination;
using AI4E.Messaging.Remote;
using AI4E.Messaging.Routing;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Messaging
{
    public static class RemoteMessagingBuilderExtensions
    {
        public static IMessagingBuilder UseRemoteMessaging<TAddress>(this IMessagingBuilder messagingBuilder)
        {
            var services = messagingBuilder.Services;

            // services.AddCoordinationService(); // TODO: Instead check for availability and throw if not available when running.
            services.TryAddSingleton<IEndPointMap<TAddress>, EndPointMap<TAddress>>();
            services.TryAddSingleton<IRouteEndPointScheduler<TAddress>, RouteEndPointScheduler<TAddress>>();

            messagingBuilder.UseRouteManager<RemoteRouteManager>();
            messagingBuilder.UseRoutingSystem<RemoteRoutingSystem<TAddress>>();
            return messagingBuilder;
        }

        public static IMessagingBuilder UsePhysicalEndPoint<TAddress, TPhysicalEndPoint>(this IMessagingBuilder messagingBuilder)
            where TPhysicalEndPoint : class, IPhysicalEndPoint<TAddress>
        {
            var services = messagingBuilder.Services;
            services.AddPhysicalEndPoint<TAddress, TPhysicalEndPoint>();
            return messagingBuilder.UseRemoteMessaging<TAddress>();
        }

        public static IMessagingBuilder UseTcpEndPoint(this IMessagingBuilder messagingBuilder)
        {
            var services = messagingBuilder.Services;
            services.AddTcpEndPoint();

            return messagingBuilder.UseRemoteMessaging<IPEndPoint>();
        }
    }
}
