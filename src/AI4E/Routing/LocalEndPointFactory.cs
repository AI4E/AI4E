/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        LocalEndPointFactory.cs 
 * Types:           AI4E.Routing.LocalEndPointFactory'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Routing
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

        public ILocalEndPoint<TAddress> CreateLocalEndPoint(IEndPointManager<TAddress> endPointManager,
                                                            IRemoteEndPointManager<TAddress> remoteEndPointManager,
                                                            IPhysicalEndPoint<TAddress> physicalEndPoint,
                                                            EndPointRoute route)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (remoteEndPointManager == null)
                throw new ArgumentNullException(nameof(remoteEndPointManager));

            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            var logger = _serviceProvider.GetService<ILogger<LocalEndPoint<TAddress>>>();

            return new LocalEndPoint<TAddress>(endPointManager, remoteEndPointManager, physicalEndPoint, route, _messageCoder, _routeManager, logger);
        }
    }
}
