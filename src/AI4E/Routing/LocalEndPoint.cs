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
        private readonly AsyncProcess _receiveProcess;

        public LocalEndPoint(IEndPointManager<TAddress> endPointManager,
                             IRemoteEndPointManager<TAddress> remoteEndPointManager,
                             IPhysicalEndPoint<TAddress> physicalEndPoint,
                             EndPointRoute route,
                             IMessageCoder<TAddress> messageCoder,
                             IRouteMap<TAddress> routeManager,
                             ILogger<LocalEndPoint<TAddress>> logger)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (remoteEndPointManager == null)
                throw new ArgumentNullException(nameof(remoteEndPointManager));

            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            _endPointManager = endPointManager;
            _remoteEndPointManager = remoteEndPointManager;
            PhysicalEndPoint = physicalEndPoint;
            Route = route;
            _messageCoder = messageCoder;
            _routeManager = routeManager;
            _logger = logger;
            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public EndPointRoute Route { get; }
        public TAddress LocalAddress => PhysicalEndPoint.LocalAddress;
        public IPhysicalEndPoint<TAddress> PhysicalEndPoint { get; }

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Map local end-point '{Route}' to physical end-point {LocalAddress}.");

            for (var waitTime = TimeSpan.FromSeconds(2); true; waitTime = new TimeSpan(waitTime.Ticks * 2))
            {
                try
                {
                    await _routeManager.MapRouteAsync(Route, LocalAddress, cancellation);
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

            await _receiveProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public bool IsDisposed => _disposeHelper.IsDisposed;

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
            await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);

            async Task UnmapAsync()
            {
                _logger?.LogDebug($"Unmap local end-point '{Route}' from physical end-point {LocalAddress}.");

                await _routeManager.UnmapRouteAsync(Route, LocalAddress, cancellation: default).HandleExceptionsAsync(_logger);
            }

            async Task TerminateReception()
            {
                await _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger);
                await PhysicalEndPoint.DisposeAsync().HandleExceptionsAsync(_logger);
            }

            await Task.WhenAll(UnmapAsync(), TerminateReception());
        }

        #endregion

        #region ReceiveProcess

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    // Receive a single message
                    var message = await PhysicalEndPoint.ReceiveAsync(cancellation);

                    var (_, localAddress, remoteAddress, remoteEndPoint, localEndPoint, messageType) = _messageCoder.DecodeMessage(message);

                    Task.Run(() => HandleMessageAsync(message, localAddress, remoteAddress, remoteEndPoint, localEndPoint, messageType, cancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure in receive process for local end-point '{Route}'.");
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, TAddress localAddress, TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, MessageType messageType, CancellationToken cancellation)
        {
            if (!localAddress.Equals(LocalAddress) || !Route.Equals(localEndPoint))
            {
                await SendMisroutedAsync(remoteAddress, remoteEndPoint, localEndPoint, cancellation);
                return;
            }

            switch (messageType)
            {
                case MessageType.Message:
                    {
                        _logger?.LogTrace($"Received message from address {remoteAddress}, end-point {remoteEndPoint} for end-point {localEndPoint}.");

                        await OnReceivedAsync(message, remoteAddress, remoteEndPoint, cancellation);

                        break;
                    }
                case MessageType.EndPointNotPresent:
                    /* TODO */
                    break;

                case MessageType.ProtocolNotSupported:
                    /* TODO */
                    break;

                case MessageType.Unknown:
                default:
                    /* TODO */
                    break;
            }
        }

        private Task SendMisroutedAsync(TAddress remoteAddress, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            var message = _messageCoder.EncodeMessage(LocalAddress, remoteAddress, remoteEndPoint, localEndPoint, MessageType.Misrouted);
            return PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
        }

        #endregion

        private async Task OnReceivedAsync(IMessage message, TAddress remoteAddress, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await _rxQueue.EnqueueAsync(message, cancellation);
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
                if (Route.Equals(remoteEndPoint))
                {
                    return OnReceivedAsync(message, LocalAddress, Route, cancellation);
                }

                // TODO

                //// We are the receiver => The remote-end-point (= receiving end point) is our local-end-point.
                //if (_endPointManager.TryGetEndPoint(remoteEndPoint, out var endPoint))
                //{
                //    return endPoint.OnReceivedAsync(message, LocalAddress, Route, cancellation);
                //}
                //else
                //{
                //    _logger?.LogWarning($"Received message for end-point {remoteEndPoint} that is unavailable.");
                //    return Task.CompletedTask;
                //}
            }

            return _remoteEndPointManager.GetRemoteEndPoint(remoteEndPoint).SendAsync(message, Route, remoteAddress, cancellation);
        }
    }
}
