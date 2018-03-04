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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Modularity
{
    public sealed class EndPointManager<TAddress> : IEndPointManager, IDisposable
    {
        private readonly IRouteMap<TAddress> _routeMap;
        private readonly IRouteSerializer _routeSerializer;
        private readonly IAddressConversion<TAddress> _addressSerializer;
        private readonly ILogger<EndPointManager<TAddress>> _logger;
        private readonly IAsyncProcess _receiveProcess;
        private readonly ConcurrentDictionary<EndPointRoute, RemoteEndPoint> _remoteEndPoints = new ConcurrentDictionary<EndPointRoute, RemoteEndPoint>();
        private readonly ConcurrentDictionary<TAddress, byte> _blacklist = new ConcurrentDictionary<TAddress, byte>();
        private readonly ConcurrentDictionary<EndPointRoute, LocalEndPoint> _localEndPoints = new ConcurrentDictionary<EndPointRoute, LocalEndPoint>();

        public EndPointManager(IPhysicalEndPoint<TAddress> physicalEndPoint,
                               IRouteMap<TAddress> routeMap,
                               IRouteSerializer routeSerializer,
                               IAddressConversion<TAddress> addressSerializer,
                               ILogger<EndPointManager<TAddress>> logger)
        {
            if (physicalEndPoint == null)
                throw new ArgumentNullException(nameof(physicalEndPoint));

            if (routeMap == null)
                throw new ArgumentNullException(nameof(routeMap));

            if (routeSerializer == null)
                throw new ArgumentNullException(nameof(routeSerializer));

            if (addressSerializer == null)
                throw new ArgumentNullException(nameof(addressSerializer));

            PhysicalEndPoint = physicalEndPoint;
            _routeMap = routeMap;
            _routeSerializer = routeSerializer;
            _addressSerializer = addressSerializer;
            _logger = logger;
            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _receiveProcess.Start();
        }

        public TAddress LocalAddress => PhysicalEndPoint.LocalAddress;
        private IPhysicalEndPoint<TAddress> PhysicalEndPoint { get; }

        private RemoteEndPoint GetRemoteEndPoint(EndPointRoute route)
        {
            return _remoteEndPoints.GetOrAdd(route, _ => new RemoteEndPoint(this, route));
        }

        public void AddEndPoint(EndPointRoute route)
        {
            _logger?.LogInformation($"Physical-end-point '{LocalAddress}': Registering end-point '{route}'.");
            _localEndPoints.AddOrUpdate(route, _ => new LocalEndPoint(this, route), (_, current) => current);
        }

        public void RemoveEndPoint(EndPointRoute route)
        {
            if (_localEndPoints.TryRemove(route, out var localEndPoint))
            {
                _logger?.LogInformation($"Physical-end-point '{LocalAddress}': Unrgistering end-point '{route}'.");
                localEndPoint.Dispose();
            }
        }

        public Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (remoteEndPoint == null)
                throw new ArgumentNullException(nameof(remoteEndPoint));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            return GetRemoteEndPoint(remoteEndPoint).SendAsync(message, localEndPoint, cancellation);
        }

        public Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var remoteAddress = default(TAddress);
            var remoteEndPoint = default(EndPointRoute);
            var localEndPoint = default(EndPointRoute);

            using (var stream = request.PushFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                // Skip the message type
                reader.ReadInt32();

                var remoteRouteLength = reader.ReadInt32();
                var remoteRouteBytes = reader.ReadBytes(remoteRouteLength);

                var remoteAddressLength = reader.ReadInt32();
                var remoteAddressBytes = reader.ReadBytes(remoteAddressLength);

                var localRouteLength = reader.ReadInt32();
                var localRouteBytes = reader.ReadBytes(localRouteLength);

                remoteAddress = _addressSerializer.DeserializeAddress(remoteAddressBytes);
                remoteEndPoint = _routeSerializer.DeserializeRoute(remoteRouteBytes);
                localEndPoint = _routeSerializer.DeserializeRoute(localRouteBytes);
            }

            // If we are the sender, we can short-circuit
            if (remoteAddress.Equals(LocalAddress))
            {
                if (_localEndPoints.TryGetValue(remoteEndPoint, out var endPoint))
                {
                    return endPoint.ReceivedAsync(response, cancellation);
                }
                else
                {
                    _logger?.LogWarning($"Physical-end-point {LocalAddress}: Received message for end-point {remoteEndPoint} that is unavailable.");
                    return Task.CompletedTask;
                }
            }

            return GetRemoteEndPoint(remoteEndPoint).SendAsync(response, localEndPoint, remoteAddress, cancellation);
        }

        public Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (_localEndPoints.TryGetValue(localEndPoint, out var endPoint))
            {
                return endPoint.ReceiveAsync(cancellation);
            }
            else
            {
                throw new InvalidOperationException("The specified end-point is unavailable.");
            }
        }

        #region RX/TX

        private async Task<(IMessage message, TAddress address, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, MessageType messageType)> ReceiveAsync(CancellationToken cancellation)
        {
            // Receive a single message
            var message = await PhysicalEndPoint.ReceiveAsync(cancellation);

            var messageType = default(MessageType);

            // Decode first message frame
            using (var frameStream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                messageType = (MessageType)reader.ReadInt32();

                var remoteRouteLength = reader.ReadInt32();
                var remoteRouteBytes = reader.ReadBytes(remoteRouteLength);
                var remoteRoute = _routeSerializer.DeserializeRoute(remoteRouteBytes);

                var remoteAddressLength = reader.ReadInt32();
                var remoteAddressBytes = reader.ReadBytes(remoteAddressLength);
                var remoteAddress = _addressSerializer.DeserializeAddress(remoteAddressBytes);

                var localRouteLength = reader.ReadInt32();
                var localRouteBytes = reader.ReadBytes(localRouteLength);
                var localRoute = _routeSerializer.DeserializeRoute(localRouteBytes);

                var localAddressLength = reader.ReadInt32();
                var localAddressBytes = reader.ReadBytes(localAddressLength);
                var localAddress = _addressSerializer.DeserializeAddress(localAddressBytes);

                if (!localAddress.Equals(LocalAddress))
                {
                    await SendMisroutedAsync(remoteAddress, remoteRoute, localRoute, cancellation);
                }

                _blacklist.TryRemove(remoteAddress, out _);

                return (message, remoteAddress, remoteRoute, localRoute, messageType);
            }
        }

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            _logger?.LogDebug($"End-point-manager '{LocalAddress}': Started receive process.'");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, address, remoteEndPoint, localEndPoint, messageType) = await ReceiveAsync(cancellation);

                    Task.Run(() => HandleMessageAsync(message, address, remoteEndPoint, localEndPoint, messageType, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"End-point-manager '{LocalAddress}': Failure while receiving message.");
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, TAddress address, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, MessageType messageType, CancellationToken cancellation)
        {
            switch (messageType)
            {
                case MessageType.Message:

                    if (_localEndPoints.TryGetValue(localEndPoint, out var endPoint))
                    {
                        await endPoint.ReceivedAsync(message, cancellation);
                    }
                    else
                    {
                        await SendRouteNotPresentAsync(address, remoteEndPoint, localEndPoint, cancellation);
                    }

                    break;

                case MessageType.ProtocolNotSupported:
                    _logger?.LogWarning($"End-point-manager '{LocalAddress}': Protocol not supported.");
                    break;

                case MessageType.RouteNotPresent:
                    _logger?.LogWarning($"End-point-manager '{LocalAddress}': Route not present: {remoteEndPoint}.");
                    break;

                default:
                    await SendProtocolNotSupportedAsync(address, remoteEndPoint, localEndPoint, cancellation);
                    break;
            }
        }

        private async Task SendAsync(IMessage message,
                               TAddress address,
                               EndPointRoute remoteEndPoint,
                               EndPointRoute localEndPoint,
                               MessageType messageType,
                               CancellationToken cancellation)
        {
            var localAddress = _addressSerializer.SerializeAddress(LocalAddress);
            var localRoute = _routeSerializer.SerializeRoute(localEndPoint);
            var remoteAddress = _addressSerializer.SerializeAddress(address);
            var remoteRoute = _routeSerializer.SerializeRoute(remoteEndPoint);

            using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
            using (var writer = new BinaryWriter(frameStream))
            {
                Console.WriteLine("z");
                Console.WriteLine("z");
                Console.WriteLine("z");
                Console.WriteLine("z");

                writer.Write((int)messageType);        // Message type            -- 4 Byte
                writer.Write(localRoute.Length);       // Local route length      -- 4 Byte
                writer.Write(localRoute);              // Local route             -- (Local route length Byte)
                writer.Write(localAddress.Length);     // Local address length    -- 4 Byte
                writer.Write(localAddress);            // Local address           -- (Local address length Byte)
                writer.Write(remoteRoute.Length);      // Remote route length     -- 4 Byte
                writer.Write(remoteRoute);             // Remote route            -- (Remote route length Byte)
                writer.Write(remoteAddress.Length);    // Remote address length   -- 4 Byte
                writer.Write(remoteAddress);           // Remote address          -- (Remote address length Byte)   
            }

            try
            {
                await PhysicalEndPoint.SendAsync(message, address, cancellation);
            }
            catch
            {
                // We must pop the frame from the message or we send it twice, the next time we send the message.
                message.PopFrame();
                throw;
            }
        }

        private Task SendAsync(TAddress address,
                               EndPointRoute remoteEndPoint,
                               EndPointRoute localEndPoint,
                               MessageType messageType,
                               CancellationToken cancellation)
        {
            return SendAsync(new Message(), address, remoteEndPoint, localEndPoint, messageType, cancellation);
        }

        private Task SendProtocolNotSupportedAsync(TAddress address, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            return SendAsync(address, remoteEndPoint, localEndPoint, MessageType.ProtocolNotSupported, cancellation);
        }

        private Task SendRouteNotPresentAsync(TAddress address, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            return SendAsync(address, remoteEndPoint, localEndPoint, MessageType.RouteNotPresent, cancellation);
        }

        private Task SendMisroutedAsync(TAddress address, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            return SendAsync(address, remoteEndPoint, localEndPoint, MessageType.Misrouted, cancellation);
        }

        #endregion

        public void Dispose()
        {
            _receiveProcess.Terminate();
        }

        private sealed class LocalEndPoint : IAsyncDisposable
        {
            private readonly EndPointManager<TAddress> _manager;
            private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
            private readonly IAsyncProcess _mapProcess;

            public LocalEndPoint(EndPointManager<TAddress> manager, EndPointRoute route)
            {
                if (manager == null)
                    throw new ArgumentNullException(nameof(manager));

                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                _manager = manager;
                Route = route;
                _mapProcess = new AsyncProcess(MapProcess);
                _mapProcess.Start();
            }

            private EndPointRoute Route { get; }
            private ILogger Logger => _manager._logger;
            private IRouteMap<TAddress> RouteMap => _manager._routeMap;

            private async Task MapProcess(CancellationToken cancellation)
            {
                Logger?.LogDebug($"Physical-end-point {_manager.LocalAddress}: Starting map process for local end-point '{Route}'.");

                var leaseLength = TimeSpan.FromSeconds(30);
                var leaseLenghtHalf = new TimeSpan(leaseLength.Ticks / 2);

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        for (var waitTime = TimeSpan.FromSeconds(2); true; waitTime = new TimeSpan(waitTime.Ticks * 2))
                        {
                            try
                            {
                                Logger?.LogDebug($"Physical-end-point {_manager.LocalAddress}: Map local end-point '{Route}'.");
                                await RouteMap.MapRouteAsync(Route, _manager.LocalAddress, DateTime.Now + leaseLength, cancellation);
                                break;
                            }
                            catch
                            {
                                await Task.Delay(waitTime, cancellation);
                                continue;
                            }
                        }

                        await Task.Delay(leaseLenghtHalf, cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception exc)
                    {
                        Logger?.LogWarning(exc, $"Physical-end-point {_manager.LocalAddress}: Failure in map process for local end-point '{Route}'.");
                    }
                }
            }

            public Task ReceivedAsync(IMessage message, CancellationToken cancellation)
            {
                Logger?.LogDebug($"Physical-end-point {_manager.LocalAddress}: Received message for local end-point '{Route}'.");
                return _rxQueue.EnqueueAsync(message, cancellation);
            }

            public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                return _rxQueue.DequeueAsync(cancellation);
            }

            #region Disposal

            private Task _disposal;
            private readonly TaskCompletionSource<byte> _disposalSource = new TaskCompletionSource<byte>();
            private readonly object _lock = new object();

            public Task Disposal => _disposalSource.Task;

            private async Task DisposeInternalAsync()
            {
                try
                {
                    _mapProcess.Terminate();

                    await RouteMap.UnmapRouteAsync(Route, _manager.LocalAddress, cancellation: default);
                }
                catch (OperationCanceledException) { }
                catch (Exception exc)
                {
                    _disposalSource.SetException(exc);
                    return;
                }

                _disposalSource.SetResult(0);
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposal == null)
                        _disposal = DisposeInternalAsync();
                }
            }

            public Task DisposeAsync()
            {
                Dispose();
                return Disposal;
            }

            #endregion
        }

        private sealed class RemoteEndPoint
        {
            private readonly EndPointManager<TAddress> _manager;
            private readonly IAsyncProcess _mapUpdateProcess;
            private readonly ConcurrentQueue<(IMessage message, EndPointRoute localEndPoint)> _txQueue = new ConcurrentQueue<(IMessage message, EndPointRoute localEndPoint)>();
            private volatile ImmutableList<TAddress> _addresses = ImmutableList<TAddress>.Empty;

            public RemoteEndPoint(EndPointManager<TAddress> manager, EndPointRoute route)
            {
                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                if (manager == null)
                    throw new ArgumentNullException(nameof(manager));

                Route = route;
                _manager = manager;
                _mapUpdateProcess = new AsyncProcess(MapUpdateProcess);
                _mapUpdateProcess.Start();
            }

            public TAddress LocalAddress => _manager.LocalAddress;
            private ILogger Logger => _manager._logger;
            private IRouteMap<TAddress> RouteMap => _manager._routeMap;
            private IAddressConversion<TAddress> AddressSerializer => _manager._addressSerializer;
            private IRouteSerializer RouteSerializer => _manager._routeSerializer;

            public EndPointRoute Route { get; }

            private async Task MapUpdateProcess(CancellationToken cancellation)
            {
                Logger?.LogDebug($"Physical-end-point '{_manager.LocalAddress}': Started map update process for remote end-point '{Route}'.");

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        await UpdateAddressList(cancellation);
                        await Task.Delay(TimeSpan.FromMinutes(5), cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception exc)
                    {
                        Logger?.LogWarning(exc, $"Physical-end-point '{_manager.LocalAddress}': Failure while updating maps for remote end-point '{Route}'.");
                    }
                }
            }

            private async Task UpdateAddressList(CancellationToken cancellation)
            {
                var addresses = new List<TAddress>(await RouteMap.GetMapsAsync(Route, cancellation));
                addresses.Shuffle();
                _addresses = addresses.ToImmutableList();

                Logger?.LogDebug($"Physical-end-point '{_manager.LocalAddress}': Updated maps for remote end-point '{Route}'.");

                var queue = new ConcurrentQueue<(IMessage message, EndPointRoute localEndPoint)>();

                while (_txQueue.TryDequeue(out var queueEntry))
                {
                    await SendInternalAsync(queueEntry.message, queueEntry.localEndPoint, queue, updateAddressesOnFailure: false, cancellation);
                }

                while (queue.TryDequeue(out var queueEntry))
                {
                    _txQueue.Enqueue(queueEntry);
                }
            }

            public Task SendAsync(IMessage message, EndPointRoute localEndPoint, CancellationToken cancellation)
            {
                if (Route == localEndPoint)
                {
                    Console.WriteLine("x");
                    Console.WriteLine("x");
                    Console.WriteLine("x");
                    Console.WriteLine("x");

                    var localAddress = AddressSerializer.SerializeAddress(LocalAddress);
                    var localRoute = RouteSerializer.SerializeRoute(localEndPoint);
                    var remoteAddress = AddressSerializer.SerializeAddress(LocalAddress);
                    var remoteRoute = RouteSerializer.SerializeRoute(localEndPoint);

                    using (var frameStream = message.PushFrame().OpenStream())
                    using (var writer = new BinaryWriter(frameStream))
                    {
                        writer.Write((int)MessageType.Message);// Message type            -- 4 Byte
                        writer.Write(localRoute.Length);       // Local route length      -- 4 Byte
                        writer.Write(localRoute);              // Local route             -- (Local route length Byte)
                        writer.Write(localAddress.Length);     // Local address length    -- 4 Byte
                        writer.Write(localAddress);            // Local address           -- (Local address length Byte)
                        writer.Write(remoteRoute.Length);      // Remote route length     -- 4 Byte
                        writer.Write(remoteRoute);             // Remote route            -- (Remote route length Byte)
                        writer.Write(remoteAddress.Length);    // Remote address length   -- 4 Byte
                        writer.Write(remoteAddress);           // Remote address          -- (Remote address length Byte)   
                    }

                    message.PopFrame();

                    if (_manager._localEndPoints.TryGetValue(localEndPoint, out var endPoint))
                    {
                        return endPoint.ReceiveAsync(cancellation);
                    }
                    else
                    {
                        throw new InvalidOperationException("The specified end-point is unavailable.");
                    }
                }

                return SendInternalAsync(message, localEndPoint, _txQueue, updateAddressesOnFailure: true, cancellation);
            }

            private async Task SendInternalAsync(IMessage message,
                                                 EndPointRoute localEndPoint,
                                                 ConcurrentQueue<(IMessage message, EndPointRoute localEndPoint)> queue,
                                                 bool updateAddressesOnFailure,
                                                 CancellationToken cancellation)
            {
                var blacklist = _manager._blacklist;
                foreach (var address in _addresses.Where(p => !blacklist.ContainsKey(p)))
                {
                    try
                    {
                        Logger?.LogDebug($"Physical-end-point '{_manager.LocalAddress}': Sending message to remote end-point '{Route}' with address '{address}'.");
                        await _manager.SendAsync(message, address, Route, localEndPoint, MessageType.Message, cancellation);

                        return;
                    }
                    catch (Exception exc)
                    {
                        Logger?.LogWarning(exc, $"Physical-end-point '{_manager.LocalAddress}': Failed sending message to remote end-point '{Route}' with address '{address}'.");
                        _manager._blacklist.TryAdd(address, 0);

                        continue;
                    }
                }

                Logger?.LogWarning($"Physical-end-point '{_manager.LocalAddress}': Failed sending message to remote end-point '{Route}'. No address available.");
                queue.Enqueue((message, localEndPoint));

                if (updateAddressesOnFailure)
                {
                    await UpdateAddressList(cancellation);
                }
            }

            public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, TAddress address, CancellationToken cancellation)
            {
                Logger?.LogDebug($"Physical-end-point '{_manager.LocalAddress}': Sending message to remote end-point '{Route}' with address '{address}'.");
                await _manager.SendAsync(message, address, Route, localEndPoint, MessageType.Message, cancellation);
            }
        }

        private enum MessageType : int
        {
            /// <summary>
            /// An unknown message type.
            /// </summary>
            Unknown = 0,

            /// <summary>
            /// A normal (user) message.
            /// </summary>
            Message = 1,

            /// <summary>
            /// The protocol of a received message is not supported. The payload is the seq-num of the message in raw format.
            /// </summary>
            ProtocolNotSupported = -1,

            RouteNotPresent = -2,

            Misrouted = -3
        }
    }
}
