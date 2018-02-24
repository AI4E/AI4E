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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace AI4E.Modularity.Debugging
{
    public class EndPointRouterSkeleton : JsonRpcService
    {
        private IDebugSession DebugSession => RequestContext.Features.Get<IDebugSession>();

        [JsonRpcMethod("init")]
        public Task Init(string localEndPoint)
        {
            DebugSession.Init(EndPointRoute.CreateRoute(localEndPoint));
            return Task.CompletedTask;
        }

        [JsonRpcMethod("receive")]
        public async Task<byte[]> ReceiveAsync(string localEndPoint)
        {
            var message = await DebugSession.GetEndPointRouter().ReceiveAsync(cancellation: default);

            using (var stream = new MemoryStream())
            {
                await message.WriteAsync(stream, cancellation: default);

                return stream.ToArray();
            }
        }

        [JsonRpcMethod("getRoutes")]
        public async Task<List<string>> GetRoutesAsync(string localEndPoint, string messageType)
        {
            var routes = await DebugSession.GetEndPointRouter().GetRoutesAsync(messageType, cancellation: default);

            return new List<string>(routes.Select(p => p.ToString()));
        }

        [JsonRpcMethod("registerRoute")]
        public async Task RegisterRouteAsync(string localEndPoint, string messageType)
        {
            await DebugSession.GetEndPointRouter().RegisterRouteAsync(messageType, cancellation: default);
            DebugSession.RegisterRoute(messageType);
        }

        [JsonRpcMethod("send1")]
        public async Task SendAsync(string localEndPoint, byte[] message, string route)
        {
            var m = new Message();

            using (var stream = new MemoryStream(message))
            {
                await m.ReadAsync(stream, cancellation: default);
            }

            await DebugSession.GetEndPointRouter().SendAsync(m, EndPointRoute.CreateRoute(route), cancellation: default);
        }

        [JsonRpcMethod("send2")]
        public async Task SendAsync(string localEndPoint, byte[] response, byte[] request)
        {
            var m1 = new Message();

            using (var stream = new MemoryStream(response))
            {
                await m1.ReadAsync(stream, cancellation: default);
            }

            var m2 = new Message();

            using (var stream = new MemoryStream(request))
            {
                await m2.ReadAsync(stream, cancellation: default);
            }

            await DebugSession.GetEndPointRouter().SendAsync(m1, m2, cancellation: default);
        }

        [JsonRpcMethod("unregisterRoute")]
        public async Task UnregisterRouteAsync(string localEndPoint, string messageType)
        {
            await DebugSession.GetEndPointRouter().UnregisterRouteAsync(messageType, cancellation: default);
            DebugSession.UnregisterRoute(messageType);
        }

        [JsonRpcMethod("renewLease")]
        public Task RenewLease(string localEndPoint)
        {
            DebugSession.GetEndPointRouter();

            return Task.CompletedTask;
        }
    }
}
