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

using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;

namespace AI4E.Messaging.Routing
{
    internal sealed class RouteEndPointReceiveResult : IRouteEndPointReceiveResult
    {
        private readonly ValueTaskCompletionSource<RouteMessageHandleResult> _resultSource;

        public RouteEndPointReceiveResult(
            Message message,
            RouteEndPointAddress remoteEndPoint,
            CancellationToken cancellation)
        {
            Message = message;
            RemoteEndPoint = remoteEndPoint;
            Cancellation = cancellation;
            _resultSource = ValueTaskCompletionSource.Create<RouteMessageHandleResult>();
        }

        public CancellationToken Cancellation { get; }

        public Message Message { get; }

        public RouteEndPointAddress RemoteEndPoint { get; }

        public ValueTask<RouteMessageHandleResult> Result => _resultSource.Task;

        public ValueTask SendResultAsync(RouteMessageHandleResult result)
        {
            _resultSource.TrySetResult(result);
            return default;
        }

        public ValueTask SendCancellationAsync()
        {
            _resultSource.TrySetCanceled();
            return default;
        }

        public ValueTask SendAckAsync()
        {
            return SendResultAsync(
                new RouteMessageHandleResult(routeMessage: default, handled: true));
        }
    }
}
