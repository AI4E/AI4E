using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.EndPointManagement
{
    public sealed class LocalEndPointFactory<TAddress> : ILocalEndPointFactory<TAddress>
    {
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly IServiceProvider _serviceProvider;

        public LocalEndPointFactory(IMessageCoder<TAddress> messageCoder,
                                    IRouteMap<TAddress> routeManager,
                                    IServiceProvider serviceProvider)
        {
            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _messageCoder = messageCoder;
            _routeManager = routeManager;
            _serviceProvider = serviceProvider;
        }

        public ILocalEndPoint<TAddress> CreateLocalEndPoint(IEndPointManager<TAddress> endPointManager, IRemoteEndPointManager<TAddress> remoteEndPointManager, EndPointRoute route)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (remoteEndPointManager == null)
                throw new ArgumentNullException(nameof(remoteEndPointManager));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            var logger = _serviceProvider.GetService<ILogger<LocalEndPoint<TAddress>>>();

            return new LocalEndPoint<TAddress>(endPointManager, remoteEndPointManager, route, _messageCoder, _routeManager, logger);
        }
    }
}
