/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.Messaging.Routing
{
    public sealed class MessageRouterFactory : IMessageRouterFactory
    {
        private readonly IRouteManager _routeManager;
        private readonly IRoutingSystem _routingSystem;
        private readonly ILoggerFactory? _loggerFactory;

        private readonly MessagingOptions _options;

        public MessageRouterFactory(IRouteManager routeManager,
                                    IRoutingSystem routingSystem,
                                    IOptions<MessagingOptions> optionsProvider,
                                    ILoggerFactory? loggerFactory = null)
        {
            if (routeManager is null)
                throw new ArgumentNullException(nameof(routeManager));

            if (routingSystem is null)
                throw new ArgumentNullException(nameof(routingSystem));

            if (optionsProvider is null)
                throw new ArgumentNullException(nameof(optionsProvider));

            _routeManager = routeManager;
            _routingSystem = routingSystem;
            _loggerFactory = loggerFactory;

            _options = optionsProvider.Value ?? new MessagingOptions();
        }

        public ValueTask<RouteEndPointAddress> GetDefaultEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<RouteEndPointAddress>(_options.LocalEndPoint);
        }

        public async ValueTask<IMessageRouter> CreateMessageRouterAsync(
            RouteEndPointAddress endPoint,
            IRouteMessageHandler routeMessageHandler,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (routeMessageHandler is null)
                throw new ArgumentNullException(nameof(routeMessageHandler));

            var routeEndPoint = await _routingSystem.GetEndPointAsync(endPoint, cancellation);
            return CreateMessageRouterInternal(routeEndPoint, routeMessageHandler);
        }

        public async ValueTask<IMessageRouter> CreateMessageRouterAsync(
            IRouteMessageHandler routeMessageHandler, CancellationToken cancellation)
        {
            if (routeMessageHandler == null)
                throw new ArgumentNullException(nameof(routeMessageHandler));

            var endPoint = _options.LocalEndPoint;
            var routeEndPoint = await _routingSystem.GetEndPointAsync(endPoint, cancellation);
            return CreateMessageRouterInternal(routeEndPoint, routeMessageHandler);
        }

        private IMessageRouter CreateMessageRouterInternal(
            IRouteEndPoint endPoint,
            IRouteMessageHandler routeMessageHandler)
        {
            var logger = _loggerFactory?.CreateLogger<MessageRouter>();
            return new MessageRouter(routeMessageHandler, endPoint, _routeManager, logger);
        }
    }
}
