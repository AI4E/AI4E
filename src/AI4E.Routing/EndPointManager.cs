/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EndPointManager.cs 
 * Types:           AI4E.Routing.EndPointManager'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   10.05.2018 
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    // TODO: The logical end point must not initialize directly
    //       If the logical end point is not disposed but gc'ed, how do we unmap and terminate the process?

    public sealed class EndPointManager<TAddress> : IEndPointManager<TAddress>, IAsyncDisposable
    {
        private static readonly byte[] _emptyByteArray = new byte[0];

        #region Fields

        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly IEndPointScheduler<TAddress> _endPointScheduler;
        private readonly IRouteSerializer _routeSerializer;
        private readonly IAddressConversion<TAddress> _addressSerializer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        private readonly WeakDictionary<EndPointRoute, LogicalEndPoint> _endPoints;

        private readonly IAsyncProcess _sendProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        // A buffer for messages to send. 
        // Messages are not sent directly to the remote end point but stored and processed one after another by a seperate async process. 
        // This enables to send again a messages that no physical end point can be found for currently or the sent failed.
        private readonly AsyncProducerConsumerQueue<(IMessage message, EndPointRoute localEndPoint, EndPointRoute remoteEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)> _txQueue;

        #endregion

        #region C'tor

        public EndPointManager(IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                               IRouteMap<TAddress> routeManager,
                               IEndPointScheduler<TAddress> endPointScheduler,
                               IRouteSerializer routeSerializer,
                               IAddressConversion<TAddress> addressSerializer,
                               IServiceProvider serviceProvider,
                               ILogger<EndPointManager<TAddress>> logger)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            if (endPointScheduler == null)
                throw new ArgumentNullException(nameof(endPointScheduler));

            if (routeSerializer == null)
                throw new ArgumentNullException(nameof(routeSerializer));

            if (addressSerializer == null)
                throw new ArgumentNullException(nameof(addressSerializer));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _endPointMultiplexer = endPointMultiplexer;
            _routeManager = routeManager;
            _endPointScheduler = endPointScheduler;
            _routeSerializer = routeSerializer;
            _addressSerializer = addressSerializer;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _endPoints = new WeakDictionary<EndPointRoute, LogicalEndPoint>();

            _txQueue = new AsyncProducerConsumerQueue<(IMessage message, EndPointRoute localEndPoint, EndPointRoute RemoteEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)>();

            _sendProcess = new AsyncProcess(SendProcess);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        #endregion

        public TAddress LocalAddress => _endPointMultiplexer.LocalAddress;

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _sendProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Gets a task that represents the disposal of the type.
        /// </summary>
        public Task Disposal => _disposeHelper.Disposal;

        private async Task DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);
            await _sendProcess.TerminateAsync().HandleExceptionsAsync(_logger);
        }

        /// <summary>
        /// Disposes of the type.
        /// </summary>
        /// <remarks>
        /// This method does not block but instead only initiates the disposal without actually waiting till disposal is completed.
        /// </remarks>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        /// <summary>
        /// Asynchronously disposes of the type.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method initiates the disposal and returns a task that represents the disposal of the type.
        /// </remarks>
        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        #endregion

        public ILogicalEndPoint<TAddress> GetLogicalEndPoint(EndPointRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            var result = CreateLogicalEndPoint(route); // TODO: The logical end point must not initialize directly;

            if (_endPoints.GetOrAdd(route, result) != result)
            {
                throw new Exception("End point already present!"); // TODO
            }

            return result;
        }

        ILogicalEndPoint IEndPointManager.GetLogicalEndPoint(EndPointRoute route)
        {
            return GetLogicalEndPoint(route);
        }

        private LogicalEndPoint CreateLogicalEndPoint(EndPointRoute route)
        {
            var physicalEndPoint = GetMultiplexPhysicalEndPoint(route);
            var logger = _serviceProvider.GetService<ILogger<LogicalEndPoint>>();
            var result = new LogicalEndPoint(this, physicalEndPoint, route, _routeManager, logger);

            Assert(result != null);
            return result;
        }

        private IPhysicalEndPoint<TAddress> GetMultiplexPhysicalEndPoint(EndPointRoute route)
        {
            var result = _endPointMultiplexer.GetPhysicalEndPoint("end-points/" + route.Route);
            Assert(result != null);
            return result;
        }

        #region Transmission

        private async Task SendAsync(IMessage message, EndPointRoute localEndPoint, EndPointRoute remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (remoteAddress == null)
                throw new ArgumentNullException(nameof(remoteAddress));

            if (remoteAddress.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(remoteAddress));

            await _initializationHelper.Initialization.WithCancellation(cancellation);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    await SendInternalAsync(message, localEndPoint, remoteEndPoint, remoteAddress, combinedCancellation);
                }
                catch (OperationCanceledException exc) when (!cancellation.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName, exc);
                }
            }
        }

        private async Task SendAsync(IMessage message, EndPointRoute localEndPoint, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            await _initializationHelper.Initialization.WithCancellation(cancellation);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var tcs = new TaskCompletionSource<object>();

                    await _txQueue.EnqueueAsync((message, localEndPoint, remoteEndPoint, attempt: 1, tcs, combinedCancellation), combinedCancellation);

                    await tcs.Task.WithCancellation(combinedCancellation);
                }
                catch (OperationCanceledException exc) when (!cancellation.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName, exc);
                }
            }
        }

        private IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica)
        {
            return _endPointScheduler.Schedule(replica);
        }

        private async Task SendProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, localEndPoint, remoteEndPoint, attempt, tcs, sendCancellation) = await _txQueue.DequeueAsync(cancellation);

                    if (attempt == 1)
                    {
                        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, sendCancellation);

                        sendCancellation = cts.Token;
                    }

                    Task.Run(() => SendInternalAsync(message, localEndPoint, remoteEndPoint, attempt, tcs, sendCancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure on sending message to remote.");
                }
            }
        }

        private async Task Reschedule(IMessage message, EndPointRoute localEndPoint, EndPointRoute remoteEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            // Calculate wait time in seconds
            var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

            await Task.Delay(timeToWait);

            await _txQueue.EnqueueAsync((message, localEndPoint, remoteEndPoint, attempt + 1, tcs, cancellation), cancellation);
        }

        // Adapted from: https://stackoverflow.com/questions/383587/how-do-you-do-integer-exponentiation-in-c
        private static int Pow(int x, int pow)
        {
            if (pow < 0)
                throw new ArgumentOutOfRangeException(nameof(pow));

            var result = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    result *= x;
                x *= x;
                pow >>= 1;
            }

            if (result < 0)
                return int.MaxValue;

            return result;
        }

        private async Task SendInternalAsync(IMessage message, EndPointRoute localEndPoint, EndPointRoute remoteEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            try
            {
                var replica = await _routeManager.GetMapsAsync(remoteEndPoint, cancellation);

                replica = Schedule(replica);

                foreach (var singleReplica in replica)
                {
                    try
                    {
                        await SendAsync(message, localEndPoint, remoteEndPoint, singleReplica, cancellation);
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        tcs.TrySetResult(null);
                    }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, "Exception occured while passing a message to the remote end.");
                    }

                    return;
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                try
                {
                    tcs.TrySetCanceled(cancellation);
                }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, "Exception occured while passing a message to the remote end.");
                }

                return;
            }
            catch (Exception exc)
            {
                _logger?.LogWarning(exc, "Exception occured while passing a message to the remote end.");
            }

            Reschedule(message, localEndPoint, remoteEndPoint, attempt, tcs, cancellation).HandleExceptions(_logger);
        }

        private async Task SendInternalAsync(IMessage message, EndPointRoute localEndPoint, EndPointRoute remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            var frameIdx = message.FrameIndex;
            EncodeMessage(message, new DecodedMessage(remoteAddress, localEndPoint, remoteEndPoint, MessageType.Message));

            try
            {
                var physicalEndPoint = GetMultiplexPhysicalEndPoint(remoteEndPoint);

                await physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PopFrame();
                Assert(frameIdx == message.FrameIndex);
                throw;
            }
        }

        #endregion

        #region Coding

        private readonly struct DecodedMessage
        {
            public DecodedMessage(TAddress txAddress,
                EndPointRoute txEndPoint,
                EndPointRoute rxEndPoint,
                MessageType messageType)
            {
                TxAddress = txAddress;
                TxEndPoint = txEndPoint;
                RxEndPoint = rxEndPoint;
                MessageType = messageType;
            }

            public TAddress TxAddress { get; }
            public EndPointRoute TxEndPoint { get; }
            public EndPointRoute RxEndPoint { get; }
            public MessageType MessageType { get; }
        }

        private void DecodeMessage(IMessage message, out DecodedMessage decodedMessage)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = default(MessageType);
            var frameIdx = message.FrameIndex;

            byte[] rxEndPointBytes,txEndPointBytes, txAddressBytes;

            try
            {
                using (var frameStream = message.PopFrame().OpenStream())
                using (var reader = new BinaryReader(frameStream))
                {
                    messageType = (MessageType)reader.ReadInt32();

                    var txEndPointLength = reader.ReadInt32();
                    txEndPointBytes = reader.ReadBytes(txEndPointLength);

                    var txAddressLength = reader.ReadInt32();
                    txAddressBytes = reader.ReadBytes(txAddressLength);

                    var rxEndPointLength = reader.ReadInt32();
                    rxEndPointBytes = reader.ReadBytes(rxEndPointLength);
                }
            }
            catch when (message.FrameIndex != frameIdx)
            {
                message.PushFrame();
                Assert(message.FrameIndex == frameIdx);
                throw;
            }

            var rxEndPoint = rxEndPointBytes.Length > 0 ? _routeSerializer.DeserializeRoute(rxEndPointBytes) : default;
            var txEndPoint = txEndPointBytes.Length > 0 ? _routeSerializer.DeserializeRoute(txEndPointBytes) : default;
            var txAddress = txAddressBytes.Length > 0 ? _addressSerializer.DeserializeAddress(txAddressBytes) : default;

            decodedMessage = new DecodedMessage(txAddress, txEndPoint, rxEndPoint, messageType);
        }

        private void EncodeMessage(IMessage message, in DecodedMessage decodedMessage)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var serializedTxAddress = decodedMessage.TxAddress == null || decodedMessage.TxAddress.Equals(default) ? _emptyByteArray : _addressSerializer.SerializeAddress(decodedMessage.TxAddress);
            var serializedTxEndPoint = decodedMessage.TxEndPoint == null ? _emptyByteArray : _routeSerializer.SerializeRoute(decodedMessage.TxEndPoint);
            var serializedRxEndPoint = decodedMessage.RxEndPoint == null ? _emptyByteArray : _routeSerializer.SerializeRoute(decodedMessage.RxEndPoint);
            var frameIdx = message.FrameIndex;

            try
            {
                using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
                using (var writer = new BinaryWriter(frameStream))
                {
                    writer.Write((int)decodedMessage.MessageType);   // Message type        -- 4 Byte

                    writer.Write(serializedTxEndPoint.Length);       // Tx end-point length -- 4 Byte
                    writer.Write(serializedTxEndPoint);              // Tx end-point        -- (Local route length Byte)

                    writer.Write(serializedTxAddress.Length);        // Tx address length   -- 4 Byte
                    writer.Write(serializedTxAddress);               // Tx address          -- (Local address length Byte)

                    writer.Write(serializedRxEndPoint.Length);       // Rx end-point length -- 4 Byte
                    writer.Write(serializedRxEndPoint);              // Rx end-point        -- (Remote route length Byte)
                }
            }
            catch when (message.FrameIndex != frameIdx)
            {
                message.PopFrame();
                Assert(message.FrameIndex == frameIdx);
                throw;
            }
        }

        #endregion

        private sealed class LogicalEndPoint : ILogicalEndPoint<TAddress>
        {
            private readonly EndPointManager<TAddress> _endPointManager;
            private readonly IRouteMap<TAddress> _routeManager;
            private readonly ILogger<LogicalEndPoint> _logger;

            private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
            private readonly AsyncInitializationHelper _initializationHelper;
            private readonly AsyncDisposeHelper _disposeHelper;
            private readonly IAsyncProcess _receiveProcess;

            public LogicalEndPoint(EndPointManager<TAddress> endPointManager,
                                 IPhysicalEndPoint<TAddress> physicalEndPoint,
                                 EndPointRoute route,
                                 IRouteMap<TAddress> routeManager,
                                 ILogger<LogicalEndPoint> logger)
            {
                if (endPointManager == null)
                    throw new ArgumentNullException(nameof(endPointManager));

                if (physicalEndPoint == null)
                    throw new ArgumentNullException(nameof(physicalEndPoint));

                if (route == null)
                    throw new ArgumentNullException(nameof(route));

                if (routeManager == null)
                    throw new ArgumentNullException(nameof(routeManager));

                _endPointManager = endPointManager;
                PhysicalEndPoint = physicalEndPoint;
                EndPoint = route;
                _routeManager = routeManager;
                _logger = logger;
                _receiveProcess = new AsyncProcess(ReceiveProcess);
                _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
                _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            }

            public EndPointRoute EndPoint { get; }
            public TAddress LocalAddress => PhysicalEndPoint.LocalAddress;
            public IPhysicalEndPoint<TAddress> PhysicalEndPoint { get; }

            #region Initialization

            public Task Initialization => _initializationHelper.Initialization;

            private async Task InitializeInternalAsync(CancellationToken cancellation)
            {
                _logger?.LogDebug($"Map local end-point '{EndPoint}' to physical end-point {LocalAddress}.");

                try
                {
                    await _routeManager.MapRouteAsync(EndPoint, LocalAddress, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Failure in map process for local end-point '{EndPoint}'.");

                    throw;
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
                    _logger?.LogDebug($"Unmap local end-point '{EndPoint}' from physical end-point {LocalAddress}.");

                    await _routeManager.UnmapRouteAsync(EndPoint, LocalAddress, cancellation: default).HandleExceptionsAsync(_logger);
                }

                async Task TerminateReception()
                {
                    await _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger);
                    await PhysicalEndPoint.DisposeIfDisposableAsync().HandleExceptionsAsync(_logger);
                }

                await Task.WhenAll(UnmapAsync(), TerminateReception());

                _endPointManager._endPoints.TryRemove(EndPoint, this);
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
                        var (message, remoteAddress) = await PhysicalEndPoint.ReceiveAsync(cancellation);

                        Task.Run(() => HandleMessageAsync(message, remoteAddress, cancellation)).HandleExceptions(_logger);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, $"Failure in receive process for local end-point '{EndPoint}'.");
                    }
                }
            }

            private async Task HandleMessageAsync(IMessage message, TAddress remoteAddress, CancellationToken cancellation)
            {
                _endPointManager.DecodeMessage(message, out var decodedMessage);

                if (!EndPoint.Equals(decodedMessage.RxEndPoint))
                {
                    await SendMisroutedAsync(decodedMessage.TxAddress, decodedMessage.TxEndPoint, decodedMessage.RxEndPoint, cancellation);
                    return;
                }

                switch (decodedMessage.MessageType)
                {
                    case MessageType.Message:
                        {
                            _logger?.LogTrace($"Received message from address {decodedMessage.TxAddress}, end-point {decodedMessage.TxEndPoint} for end-point {decodedMessage.RxEndPoint}.");

                            await OnReceivedAsync(message, decodedMessage.TxAddress, decodedMessage.TxEndPoint, cancellation);

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
                var message = new Message();
                _endPointManager.EncodeMessage(message, new DecodedMessage(remoteAddress, localEndPoint, remoteEndPoint, MessageType.Misrouted));
                return PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }

            #endregion

            private async Task OnReceivedAsync(IMessage message, TAddress remoteAddress, EndPointRoute remoteEndPoint, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                await _rxQueue.EnqueueAsync(message, cancellation);
            }

            public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                await _initializationHelper.Initialization.WithCancellation(cancellation);

                using (await _endPointManager._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_endPointManager._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);

                    var combinedCancellation = _endPointManager._disposeHelper.CancelledOrDisposed(cancellation);

                    try
                    {
                        using (await _disposeHelper.ProhibitDisposalAsync(combinedCancellation))
                        {
                            if (_disposeHelper.IsDisposed)
                                throw new ObjectDisposedException(GetType().FullName);

                            combinedCancellation = _disposeHelper.CancelledOrDisposed(combinedCancellation);

                            try
                            {
                                return await _rxQueue.DequeueAsync(combinedCancellation);
                            }
                            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                            {
                                throw new ObjectDisposedException(GetType().FullName);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_endPointManager._disposeHelper.IsDisposed)
                    {
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);
                    }
                }
            }

            public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                await _initializationHelper.Initialization.WithCancellation(cancellation);

                using (await _endPointManager._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_endPointManager._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);

                    var combinedCancellation = _endPointManager._disposeHelper.CancelledOrDisposed(cancellation);

                    try
                    {
                        using (await _disposeHelper.ProhibitDisposalAsync(combinedCancellation))
                        {
                            if (_disposeHelper.IsDisposed)
                                throw new ObjectDisposedException(GetType().FullName);

                            combinedCancellation = _disposeHelper.CancelledOrDisposed(combinedCancellation);

                            try
                            {
                                await _endPointManager.SendAsync(message, EndPoint, remoteEndPoint, combinedCancellation);
                            }
                            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                            {
                                throw new ObjectDisposedException(GetType().FullName);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_endPointManager._disposeHelper.IsDisposed)
                    {
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);
                    }
                }
            }

            public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (remoteEndPoint == null)
                    throw new ArgumentNullException(nameof(remoteEndPoint));

                if (remoteAddress == null)
                    throw new ArgumentNullException(nameof(remoteAddress));

                if (remoteAddress == default)
                    throw new ArgumentDefaultException(nameof(remoteAddress));

                await _initializationHelper.Initialization.WithCancellation(cancellation);

                using (await _endPointManager._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_endPointManager._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);

                    var combinedCancellation = _endPointManager._disposeHelper.CancelledOrDisposed(cancellation);

                    try
                    {
                        using (await _disposeHelper.ProhibitDisposalAsync(combinedCancellation))
                        {
                            if (_disposeHelper.IsDisposed)
                                throw new ObjectDisposedException(GetType().FullName);

                            combinedCancellation = _disposeHelper.CancelledOrDisposed(combinedCancellation);

                            try
                            {
                                await SendInternalAsync(message, remoteEndPoint, remoteAddress, combinedCancellation);
                            }
                            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                            {
                                throw new ObjectDisposedException(GetType().FullName);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_endPointManager._disposeHelper.IsDisposed)
                    {
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);
                    }
                }
            }

            public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
            {
                if (response == null)
                    throw new ArgumentNullException(nameof(response));

                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                // We need to push the frame in order that the decoder can pop it
                var frameIdx = request.FrameIndex;
                request.PushFrame();

                TAddress remoteAddress;
                EndPointRoute localEndPoint, remoteEndPoint;

                try
                {
                    try
                    {
                        _endPointManager.DecodeMessage(request, out var decodedMessage);
                        remoteAddress = decodedMessage.TxAddress;
                        localEndPoint = decodedMessage.RxEndPoint;
                        remoteEndPoint = decodedMessage.TxEndPoint;
                    }
                    catch (Exception exc)
                    {
                        throw new ArgumentException("The message is not formatted as expected.", nameof(request), exc);
                    }

                    Assert(remoteAddress != null);
                    Assert(remoteEndPoint != null);
                    Assert(localEndPoint != null);
                    Assert(frameIdx == request.FrameIndex);
                }
                catch when (frameIdx != request.FrameIndex)
                {
                    request.PopFrame();
                    Assert(frameIdx == request.FrameIndex);
                    throw;
                }

                if (localEndPoint != EndPoint)
                {
                    throw new InvalidOperationException("Cannot send a response from another end point than the request was received from.");
                }

                await _initializationHelper.Initialization.WithCancellation(cancellation);

                using (await _endPointManager._disposeHelper.ProhibitDisposalAsync(cancellation))
                {
                    if (_endPointManager._disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);

                    var combinedCancellation = _endPointManager._disposeHelper.CancelledOrDisposed(cancellation);

                    try
                    {
                        using (await _disposeHelper.ProhibitDisposalAsync(combinedCancellation))
                        {
                            if (_disposeHelper.IsDisposed)
                                throw new ObjectDisposedException(GetType().FullName);

                            combinedCancellation = _disposeHelper.CancelledOrDisposed(combinedCancellation);

                            try
                            {
                                await SendInternalAsync(response, remoteEndPoint, remoteAddress, combinedCancellation);
                            }
                            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                            {
                                throw new ObjectDisposedException(GetType().FullName);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (_endPointManager._disposeHelper.IsDisposed)
                    {
                        throw new ObjectDisposedException(_endPointManager.GetType().FullName);
                    }
                }
            }

            private Task SendInternalAsync(IMessage message, EndPointRoute remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
            {
                //If we are the sender, we can short-circuit
                if (remoteAddress.Equals(LocalAddress))
                {
                    if (EndPoint.Equals(remoteEndPoint))
                    {
                        return OnReceivedAsync(message, LocalAddress, EndPoint, cancellation);
                    }

                    if (_endPointManager._endPoints.TryGetValue(remoteEndPoint, out var endPoint))
                    {
                        return endPoint.OnReceivedAsync(message, LocalAddress, EndPoint, cancellation);
                    }

                    _logger?.LogWarning($"Received message for end-point {remoteEndPoint} that is unavailable.");
                    return Task.CompletedTask;
                }

                return _endPointManager.SendAsync(message, EndPoint, remoteEndPoint, remoteAddress, cancellation);
            }
        }
    }
}
