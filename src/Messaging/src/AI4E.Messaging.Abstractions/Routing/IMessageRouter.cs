/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Messaging.Routing
{
    public interface IMessageRouter : IDisposable, IAsyncDisposable
    {
        ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);

        ValueTask<IReadOnlyCollection<RouteMessage<IDispatchResult>>> RouteAsync(
            RouteHierarchy routes,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointScope localScope,
            CancellationToken cancellation = default);

        ValueTask<RouteMessage<IDispatchResult>> RouteAsync(
            Route route,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointScope remoteScope,
            RouteEndPointScope localScope,
            CancellationToken cancellation = default);

        Task RegisterRouteAsync(RouteRegistration routeRegistration, CancellationToken cancellation = default);
        Task UnregisterRouteAsync(Route route, CancellationToken cancellation = default);
        Task UnregisterRoutesAsync(bool removePersistentRoutes, CancellationToken cancellation = default);

        RouteEndPointScope CreateScope();

        bool OwnsScope(RouteEndPointScope scope);
    }

    public static class MessageRouterExtensions
    {
        public static async Task RegisterRoutesAsync(
            this IMessageRouter messageRouter,
            IEnumerable<RouteRegistration> routeRegistrations,
            CancellationToken cancellation = default)
        {
            if (routeRegistrations is null)
                throw new ArgumentNullException(nameof(routeRegistrations));

            try
            {
                await Task.WhenAll(routeRegistrations.Select(p => messageRouter.RegisterRouteAsync(p, cancellation)));
            }
            catch
            {
                // TODO: Remove all routes we registered
                throw;
            }
        }
    }
}
