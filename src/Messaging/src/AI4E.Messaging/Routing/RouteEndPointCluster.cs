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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AI4E.Messaging.Routing
{
    internal sealed class RouteEndPointCluster : IDisposable
    {
        #region Fields

        private readonly RoutingSystem _routingSystem;
        private readonly ILogger<RouteEndPointCluster>? _logger;

        private readonly Channel<RouteEndPointReceiveResult> _messages
            = Channel.CreateUnbounded<RouteEndPointReceiveResult>();

        private CancellationTokenSource? _disposalSource = new CancellationTokenSource();

        #endregion

        #region C'tor

        public RouteEndPointCluster(
            RoutingSystem routingSystem,
            RouteEndPointAddress endPoint,
            ILogger<RouteEndPointCluster>? logger)
        {
            _routingSystem = routingSystem;
            EndPoint = endPoint;
            _logger = logger;
        }

        #endregion

        public RouteEndPointAddress EndPoint { get; }

        #region Receive

        public bool TryReceive(out RouteEndPointReceiveResult result)
        {
            if (_messages.Reader.Completion.Status == TaskStatus.RanToCompletion)
            {
                throw ObjectDisposedException();
            }

            return _messages.Reader.TryRead(out result);
        }

        public async ValueTask WaitReceiveResultAsync(CancellationToken cancellation)
        {
            if (!await _messages.Reader.WaitToReadAsync(cancellation).ConfigureAwait(false))
            {
                throw ObjectDisposedException();
            }
        }

        #endregion

        #region Send

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
                var endPointCluster = this;

                if (remoteEndPoint != EndPoint)
                {
                    endPointCluster = _routingSystem.GetEndPointCluster(remoteEndPoint);

                    if (endPointCluster is null)
                        return default;
                }

                return await endPointCluster.BufferAsync(
                    message,
                    clusterNodeIdentifier,
                    checkDisposal: endPointCluster != this,
                    cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (disposal.IsCancellationRequested)
            {
                throw ObjectDisposedException();
            }
        }

        private async ValueTask<RouteMessageHandleResult> BufferAsync(
            Message message,
            ClusterNodeIdentifier clusterNodeIdentifier,
            bool checkDisposal,
            CancellationToken cancellation)
        {
            if (!checkDisposal)
            {
                return await UncheckedBufferAsync(message, clusterNodeIdentifier, cancellation)
                   .ConfigureAwait(false);
            }

            // We already protected of disposal of the sender end-point but not of the receiver end-point.
            // The case that the receiver end-point is disposed should be handled the same as if it did not exist in
            // the time of checking for its existence in prepare for delivering the message.

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
                return await UncheckedBufferAsync(message, clusterNodeIdentifier, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (disposal.IsCancellationRequested)
            {
                return default;
            }
        }

        private async ValueTask<RouteMessageHandleResult> UncheckedBufferAsync(
            Message message,
            ClusterNodeIdentifier clusterNodeIdentifier,
            CancellationToken cancellation)
        {
            if (clusterNodeIdentifier == default)
            {
                var receiveResult = new RouteEndPointReceiveResult(message, EndPoint, cancellation);
                await _messages.Writer.WriteAsync(receiveResult, cancellation).ConfigureAwait(false);
                var routeResult = await receiveResult.Result.ConfigureAwait(false);
                return routeResult;
            }

            RouteEndPoint routeEndPoint;

            lock (_mutex)
            {
                if (!_routeEndPoints.TryGetValue(clusterNodeIdentifier, out routeEndPoint!))
                {
                    return default;
                }
            }

            return await routeEndPoint.BufferAsync(message, cancellation).ConfigureAwait(false);
        }

        #endregion

        #region RouteEndPoint

        private readonly Dictionary<ClusterNodeIdentifier, RouteEndPoint> _routeEndPoints
            = new Dictionary<ClusterNodeIdentifier, RouteEndPoint>();
        private readonly object _mutex = new object();
        private long _nextClusterIdentifier = 1;


        public RouteEndPoint CreateExternalRouteEndPoint()
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

            var rawClusterNodeIdentifier = Interlocked.Increment(ref _nextClusterIdentifier);
            var clusterNodeIdentifierBytes = BitConverter.GetBytes(rawClusterNodeIdentifier);
            var clusterNodeIdentifier = ClusterNodeIdentifier.UnsafeCreateWithoutCopy(clusterNodeIdentifierBytes);
            var result = new RouteEndPoint(this, clusterNodeIdentifier);

            lock (_mutex)
            {
                _routeEndPoints[clusterNodeIdentifier] = result;
            }

            // Check for disposal again, as we may get disposed in the mean-time
            if (disposal.IsCancellationRequested)
            {
                result.Dispose();
                throw ObjectDisposedException();
            }

            return result;
        }

        internal void RemoveRouteEndPoint(RouteEndPoint routeEndPoint)
        {
            lock (_mutex)
            {
                _routeEndPoints.Remove(routeEndPoint.ClusterNodeIdentifier, routeEndPoint);

                // Call dispose while holding on the lock to prevent the situation that
                // concurrently a new route end-point is created and handed out to the caller 
                // disposing of it a small amount of time later.
                if (_routeEndPoints.Count == 0)
                {
                    Dispose();
                }
            }
        }

        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource is null)
                return;

            using (disposalSource)
            {
                // Mark us as canceled and abort all running operations.
                disposalSource.Cancel();

                // Dispose of all route end-points.
                // Be aware that there is a race of checking for disposal and adding new route end-points. There has
                // to be a double check in these places present.
                ImmutableArray<RouteEndPoint> routeEndPoints;

                lock (_mutex)
                {
                    routeEndPoints = _routeEndPoints.Values.ToImmutableArray();
                }

                foreach (var routeEndPoint in routeEndPoints)
                {
                    routeEndPoint.Dispose();
                }

                // Mark the message buffer as completed.
                _messages.Writer.Complete();

                // Remove all buffered messages.
                while (_messages.Reader.TryRead(out _)) ;

                // Remove us from the routing system.
                _routingSystem.RemoveRouteEndPoint(this);
            }
        }

        private ObjectDisposedException ObjectDisposedException()
        {
            return new ObjectDisposedException(GetType().FullName);
        }
    }
}
