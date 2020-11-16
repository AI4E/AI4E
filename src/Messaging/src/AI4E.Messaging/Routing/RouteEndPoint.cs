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
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AI4E.Messaging.Routing
{
    internal sealed class RouteEndPoint : IRouteEndPoint
    {
        private readonly RouteEndPointCluster _endPointCluster;

        private readonly Channel<RouteEndPointReceiveResult> _messages
            = Channel.CreateUnbounded<RouteEndPointReceiveResult>();

        private CancellationTokenSource? _disposalSource = new CancellationTokenSource();

        public RouteEndPoint(
            RouteEndPointCluster endPointCluster,
            ClusterNodeIdentifier clusterNodeIdentifier)
        {
            _endPointCluster = endPointCluster;
            ClusterNodeIdentifier = clusterNodeIdentifier;
        }

        public RouteEndPointAddress EndPoint => _endPointCluster.EndPoint;

        public ClusterNodeIdentifier ClusterNodeIdentifier { get; }

        public async ValueTask<IRouteEndPointReceiveResult> ReceiveAsync(
            CancellationToken cancellation = default)
        {
            var disposalSource = Volatile.Read(ref _disposalSource);

            if (disposalSource is null)
            {
                throw ObjectDisposedException();
            }

            var disposal = disposalSource.Token;

            if (disposal.IsCancellationRequested)
            {
                throw ObjectDisposedException();
            }

            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, disposal);

            try
            {
                do
                {
                    if (_messages.Reader.TryRead(out var result))
                    {
                        return result;
                    }

                    if (_endPointCluster.TryReceive(out result))
                    {
                        return result;
                    }

                    var waitForClusterMessage = _endPointCluster.WaitReceiveResultAsync(
                        cancellationTokenSource.Token).AsTask();
                    var waitForMessage = _messages.Reader.WaitToReadAsync(cancellationTokenSource.Token).AsTask();

                    await Task.WhenAny(waitForClusterMessage, waitForMessage).ConfigureAwait(false);
                }
                while (true);
            }
            catch (OperationCanceledException) when (disposal.IsCancellationRequested)
            {
                throw ObjectDisposedException();
            }
        }

        public async ValueTask<RouteMessageHandleResult> SendAsync(
            Message message,
            RouteEndPointAddress remoteEndPoint,
            ClusterNodeIdentifier clusterNodeIdentifier,
            CancellationToken cancellation = default)
        {
            var disposalSource = Volatile.Read(ref _disposalSource);

            if (disposalSource is null)
            {
                throw ObjectDisposedException();
            }

            var disposal = disposalSource.Token;

            if (disposal.IsCancellationRequested)
            {
                throw ObjectDisposedException();
            }

            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, disposal);

            try
            {
                if (remoteEndPoint == EndPoint && clusterNodeIdentifier == ClusterNodeIdentifier)
                {
                    return await UncheckedBufferAsync(message, cancellationTokenSource.Token).ConfigureAwait(false);
                }

                return await _endPointCluster.SendAsync(
                    message, remoteEndPoint, clusterNodeIdentifier, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (disposal.IsCancellationRequested)
            {
                throw ObjectDisposedException();
            }
        }

        internal async ValueTask<RouteMessageHandleResult> BufferAsync(Message message, CancellationToken cancellation)
        {
            var disposalSource = Volatile.Read(ref _disposalSource);

            if (disposalSource is null)
            {
                return default;
            }

            var disposal = disposalSource.Token;

            if (disposal.IsCancellationRequested)
            {
                return default;
            }

            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, disposal);

            try
            {
                return await UncheckedBufferAsync(message, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (disposal.IsCancellationRequested)
            {
                return default;
            }
        }

        private async ValueTask<RouteMessageHandleResult> UncheckedBufferAsync(
            Message message,
            CancellationToken cancellation)
        {
            var receiveResult = new RouteEndPointReceiveResult(message, EndPoint, cancellation);
            await _messages.Writer.WriteAsync(receiveResult, cancellation).ConfigureAwait(false);
            var routeResult = await receiveResult.Result.ConfigureAwait(false);
            return routeResult;
        }

        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource is null)
                return;

            using (disposalSource)
            {
                disposalSource.Cancel();

                _endPointCluster.RemoveRouteEndPoint(this);
            }
        }

        private ObjectDisposedException ObjectDisposedException()
        {
            return new ObjectDisposedException(GetType().FullName);
        }
    }
}
