/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        DebugRouteStore.cs 
 * Types:           AI4E.Routing.Debugging.DebugRouteStore
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Proxying;

namespace AI4E.Routing.Debugging
{
    public sealed class DebugRouteStore : IRouteStore
    {
        private IProxy<RouteStoreSkeleton> _proxy;
        private readonly ProxyHost _rpcHost;
        private readonly Task _initialization;

        public DebugRouteStore(ProxyHost rpcHost)
        {
            if (rpcHost == null)
                throw new ArgumentNullException(nameof(rpcHost));

            _rpcHost = rpcHost;
            _initialization = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _proxy = await _rpcHost.ActivateAsync<RouteStoreSkeleton>(ActivationMode.Create, cancellation: default);
        }

        public async Task<bool> AddRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.AddRouteAsync(localEndPoint, messageType, CancellationToken.None));
        }

        public async Task<bool> RemoveRouteAsync(EndPointRoute localEndPoint, string messageType, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.RemoveRouteAsync(localEndPoint, messageType, CancellationToken.None));
        }

        public async Task RemoveRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.RemoveRouteAsync(localEndPoint, CancellationToken.None));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.GetRoutesAsync(messageType, CancellationToken.None));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.GetRoutesAsync(CancellationToken.None));
        }
    }
}
