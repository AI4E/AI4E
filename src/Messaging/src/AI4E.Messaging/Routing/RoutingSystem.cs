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
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AI4E.Messaging.Routing
{
    public sealed class RoutingSystem : IRoutingSystem
    {
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<RoutingSystem>? _logger;

        private readonly Dictionary<RouteEndPointAddress, RouteEndPointCluster> _endPointClusters;

        private CancellationTokenSource? _disposalSource = new CancellationTokenSource();

        public RoutingSystem(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<RoutingSystem>();

            _endPointClusters = new Dictionary<RouteEndPointAddress, RouteEndPointCluster>();
        }

        public ValueTask<IRouteEndPoint> GetEndPointAsync(
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (IsDisposed(out var disposal))
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            var result = UncheckedGetEndPoint(endPoint);

            // Check for disposal again, as we may get disposed in the mean-time
            if (disposal.IsCancellationRequested)
            {
                result.Dispose();
            }

            return new ValueTask<IRouteEndPoint>(result);
        }

        private IRouteEndPoint UncheckedGetEndPoint(RouteEndPointAddress endPoint)
        {
            lock (_endPointClusters)
            {
                if (_endPointClusters.TryGetValue(endPoint, out var result))
                {
                    return result.CreateExternalRouteEndPoint();
                }

                var logger = _loggerFactory?.CreateLogger<RouteEndPointCluster>();
                result = new RouteEndPointCluster(this, endPoint, logger);

                try
                {
                    _endPointClusters.Add(endPoint, result);
                    return result.CreateExternalRouteEndPoint();
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }

        internal RouteEndPointCluster? GetEndPointCluster(RouteEndPointAddress endPoint)
        {
            lock (_endPointClusters)
            {
                if (!_endPointClusters.TryGetValue(endPoint, out var result))
                {
                    result = null!;
                }

                return result;
            }
        }

        internal void RemoveRouteEndPoint(RouteEndPointCluster routeEndPoint)
        {
            lock (_endPointClusters)
            {
                _endPointClusters.Remove(routeEndPoint.EndPoint, routeEndPoint);
            }
        }

        #region Disposal

        /// <inheritdoc/>
        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource is null)
                return;

            using (disposalSource)
            {
                disposalSource.Cancel();

                ImmutableArray<RouteEndPointCluster> endPointClusters;

                lock (_endPointClusters)
                {
                    endPointClusters = _endPointClusters.Values.ToImmutableArray();
                }

                foreach (var endPointCluster in endPointClusters)
                {
                    endPointCluster.Dispose();
                }
            }
        }

        private bool IsDisposed(out CancellationToken disposalToken)
        {
            var disposalSource = Volatile.Read(ref _disposalSource);

            if (disposalSource is null)
            {
                disposalToken = new CancellationToken(canceled: true);
                return true;
            }

            disposalToken = disposalSource.Token;
            if (disposalToken.IsCancellationRequested)
            {
                return true;
            }

            return false;
        }

        #endregion
    }
}
