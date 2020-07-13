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
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.Routing
{
    public interface IRouteEndPoint<TAddress> : IRouteEndPoint, IDisposable
    {
        TAddress LocalAddress { get; }

        new ValueTask<IRouteEndPointReceiveResult<TAddress>> ReceiveAsync(
            CancellationToken cancellation = default);

        ValueTask<RouteMessageHandleResult> SendAsync(
            Message message,
            RouteEndPointAddress remoteEndPoint,
            TAddress remoteAddress,
            CancellationToken cancellation = default);

        async ValueTask<IRouteEndPointReceiveResult> IRouteEndPoint.ReceiveAsync(
            CancellationToken cancellation)
        {
            return await ReceiveAsync(cancellation);
        }
    }

    public interface IRouteEndPointReceiveResult<TAddress> : IRouteEndPointReceiveResult
    {
        TAddress RemoteAddress { get; }
    }
}
