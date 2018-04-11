/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        DebugEndPointManager.cs 
 * Types:           AI4E.Routing.Debugging.DebugEndPointManager
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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Proxying;
using AI4E.Remoting;

namespace AI4E.Routing.Debugging
{
    public sealed class DebugEndPointManager : IEndPointManager
    {
        private IProxy<EndPointManagerSkeleton> _proxy;
        private readonly ProxyHost _rpcHost;
        private readonly Task _initialization;

        public DebugEndPointManager(ProxyHost rpcHost)
        {
            if (rpcHost == null)
                throw new ArgumentNullException(nameof(rpcHost));

            _rpcHost = rpcHost;
            _initialization = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _proxy = await _rpcHost.ActivateAsync<EndPointManagerSkeleton>(ActivationMode.Create, cancellation: default);
        }

        public async Task AddEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.AddEndPointAsync(route, cancellation));
        }

        public async Task RemoveEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.RemoveEndPointAsync(route, cancellation));
        }

        public async Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _initialization;

            return await _proxy.ExecuteAsync(p => p.ReceiveAsync(localEndPoint, CancellationToken.None));
        }

        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.SendAsync(message, remoteEndPoint, localEndPoint, CancellationToken.None));
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            await _initialization;

            await _proxy.ExecuteAsync(p => p.SendAsync(response, request, CancellationToken.None));
        }

        public void Dispose() { }
    }
}
