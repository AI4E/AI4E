/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        LocalEndPoint.cs 
 * Types:           AI4E.Routing.LocalEndPoint'1
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
using AI4E.Async;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Routing
{
    public class LocalEndPoint<TAddress> : ILocalEndPoint<TAddress>
    {
        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
        private readonly IEndPointManager<TAddress> _endPointManager;
        private readonly IRemoteEndPointManager<TAddress> _remoteEndPointManager;
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly ILogger<LocalEndPoint<TAddress>> _logger;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncProcess _mapProcess;

        private readonly TimeSpan _leaseLength = TimeSpan.FromSeconds(30);

        public LocalEndPoint(IEndPointManager<TAddress> endPointManager,
                             IRemoteEndPointManager<TAddress> remoteEndPointManager,
                             EndPointRoute route,
                             IMessageCoder<TAddress> messageCoder,
                             IRouteMap<TAddress> routeManager,
                             ILogger<LocalEndPoint<TAddress>> logger)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (remoteEndPointManager == null)
                throw new ArgumentNullException(nameof(remoteEndPointManager));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            _endPointManager = endPointManager;
            _remoteEndPointManager = remoteEndPointManager;
            Route = route;
            _messageCoder = messageCoder;
            _routeManager = routeManager;
            _logger = logger;
            _mapProcess = new AsyncProcess(MapProcessAsync);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            
        }

        public EndPointRoute Route { get; }
        public TAddress LocalAddress => _endPointManager.LocalAddress;
        public IPhysicalEndPoint<TAddress> PhysicalEndPoint => _endPointManager.PhysicalEndPoint;

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await MapRouteAsync(cancellation);
            await _mapProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync();

            await _mapProcess.TerminateAsync();

            await UnmapRouteAsync();
        }

        #endregion

        #region MapProcess

        private async Task MapProcessAsync(CancellationToken cancellation)
        {
            var leaseLengthHalf = new TimeSpan(_leaseLength.Ticks / 2);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    await Task.Delay(leaseLengthHalf);

                    await MapRouteAsync(cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure in map process for local end-point '{Route}'.");
                }
            }
        }

        private async Task MapRouteAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Map local end-point '{Route}' to physical end-point {LocalAddress}.");

            for (var waitTime = TimeSpan.FromSeconds(2); true; waitTime = new TimeSpan(waitTime.Ticks * 2))
            {
                try
                {
                    await _routeManager.MapRouteAsync(Route, LocalAddress, DateTime.Now + _leaseLength, cancellation);
                    break;
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure in map process for local end-point '{Route}'.");

                    await Task.Delay(waitTime, cancellation);
                    continue;
                }
            }
        }

        private async Task UnmapRouteAsync()
        {
            _logger?.LogDebug($"Unmap local end-point '{Route}' from physical end-point {LocalAddress}.");

            await _routeManager.UnmapRouteAsync(Route, LocalAddress, cancellation: default);
        }

        #endregion

        public async Task OnReceivedAsync(IMessage message, TAddress remoteAddress, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await _rxQueue.EnqueueAsync(message, cancellation);

            if (!remoteAddress.Equals(LocalAddress) || remoteEndPoint != Route)
            {
                await OnSignalledAsync(remoteAddress, cancellation);
            }
        }

        public Task OnSignalledAsync(TAddress remoteAddress, CancellationToken cancellation)
        {
            var message = _messageCoder.EncodeMessage(LocalAddress, remoteAddress, default, Route, MessageType.Request);
            return PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
        }

        public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            return _rxQueue.DequeueAsync(cancellation);
        }

        public Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            return _remoteEndPointManager.GetRemoteEndPoint(remoteEndPoint).SendAsync(message, Route, cancellation);
        }

        public Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            // If we are the sender, we can short-circuit
            if (remoteAddress.Equals(LocalAddress))
            {
                // We are the receive => The remote-end-point (= receiving end point) is our local-end-point.
                if (_endPointManager.TryGetEndPoint(remoteEndPoint, out var endPoint))
                {
                    return endPoint.OnReceivedAsync(message, LocalAddress, Route, cancellation);
                }
                else
                {
                    _logger?.LogWarning($"Received message for end-point {remoteEndPoint} that is unavailable.");
                    return Task.CompletedTask;
                }
            }

            return _remoteEndPointManager.GetRemoteEndPoint(remoteEndPoint).SendAsync(message, Route, remoteAddress, cancellation);
        }
    }
}
