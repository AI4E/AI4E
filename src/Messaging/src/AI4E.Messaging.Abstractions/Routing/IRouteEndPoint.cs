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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Messaging.Routing
{

    public interface IRouteEndPoint : IDisposable
    {
        RouteEndPointAddress EndPoint { get; }

        ClusterNodeIdentifier ClusterNodeIdentifier { get; }

        ValueTask<IRouteEndPointReceiveResult> ReceiveAsync(
            CancellationToken cancellation = default);

        ValueTask<RouteMessageHandleResult> SendAsync(
            Message message,
            RouteEndPointAddress remoteEndPoint,
            CancellationToken cancellation = default)
        {
            return SendAsync(
                message, 
                remoteEndPoint, 
                clusterIdentifier: ClusterNodeIdentifier.NoClusterNodeIdentifier, 
                cancellation);
        }

        ValueTask<RouteMessageHandleResult> SendAsync(
            Message message,
            RouteEndPointAddress remoteEndPoint,
            ClusterNodeIdentifier clusterIdentifier,
            CancellationToken cancellation = default);
    }

    public interface IRouteEndPointReceiveResult
    {
        CancellationToken Cancellation { get; }
        Message Message { get; }
        RouteEndPointAddress RemoteEndPoint { get; }

        // Send the specified response and end the request.
        ValueTask SendResultAsync(RouteMessageHandleResult result);
        ValueTask SendCancellationAsync();
        ValueTask SendAckAsync();
    }
}
