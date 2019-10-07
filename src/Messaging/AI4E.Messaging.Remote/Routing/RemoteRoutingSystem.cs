using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Remote;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Messaging.Primitives;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Messaging.Routing
{
    // TODO: Retransmission for Request and CancellationRequest messages
    public sealed class RemoteRoutingSystem<TAddress> : IRoutingSystem<TAddress>
    {
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IEndPointMap<TAddress> _endPointMap;
        private readonly IRouteEndPointScheduler<TAddress> _endPointScheduler;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<RemoteRoutingSystem<TAddress>> _logger;

        private Dictionary<RouteEndPointAddress, RouteEndPoint> _endPoints;
        private readonly object _endPointsLock = new object();

        // 0: false, 1: true
        private volatile int _isDisposed = 0;

        public RemoteRoutingSystem(
            IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
            IEndPointMap<TAddress> endPointMap,
            IRouteEndPointScheduler<TAddress> endPointScheduler,
            ILoggerFactory loggerFactory)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (endPointMap == null)
                throw new ArgumentNullException(nameof(endPointMap));

            if (endPointScheduler == null)
                throw new ArgumentNullException(nameof(endPointScheduler));

            _endPointMultiplexer = endPointMultiplexer;
            _endPointMap = endPointMap;
            _endPointScheduler = endPointScheduler;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<RemoteRoutingSystem<TAddress>>();

            _endPoints = new Dictionary<RouteEndPointAddress, RouteEndPoint>();
        }

        #region IEndPointManager

        public TAddress LocalAddress => _endPointMultiplexer.LocalAddress;

        public ValueTask<IRouteEndPoint<TAddress>> GetEndPointAsync(
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            lock (_endPointsLock)
            {
                if (_endPoints == null)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                if (!_endPoints.TryGetValue(endPoint, out var logicalEndPoint))
                {
                    logicalEndPoint = null;
                }

                return new ValueTask<IRouteEndPoint<TAddress>>(result: null);
            }
        }

        public ValueTask<IRouteEndPoint<TAddress>> CreateEndPointAsync(
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (_isDisposed != 0) // Volatile read op.
                throw new ObjectDisposedException(GetType().FullName);

            // We have to ensure that only a single logical end-point exists for each address at any given time.
            lock (_endPointsLock)
            {
                if (_endPoints == null)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                if (_endPoints.TryGetValue(endPoint, out _))
                {
                    throw new Exception("End point already present!"); // TODO
                }

                var logicalEndPoint = CreateLogicalEndPointInternal(endPoint);
                _endPoints.Add(endPoint, logicalEndPoint);
                return new ValueTask<IRouteEndPoint<TAddress>>(logicalEndPoint);
            }
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        async ValueTask<IRouteEndPoint> IRoutingSystem.GetEndPointAsync(
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            return await GetEndPointAsync(endPoint, cancellation);
        }

        async ValueTask<IRouteEndPoint> IRoutingSystem.CreateEndPointAsync(
            RouteEndPointAddress endPoint,
            CancellationToken cancellation)
        {
            return await CreateEndPointAsync(endPoint, cancellation);
        }
#endif

        #endregion

        #region Disposal

        public void Dispose()
        {
            var isDisposed = Interlocked.Exchange(ref _isDisposed, 1) != 0;

            if (!isDisposed)
            {
                lock (_endPointsLock)
                {
                    foreach (var logicalEndPoint in _endPoints.Values)
                    {
                        logicalEndPoint.Dispose();
                    }

                    _endPoints = null;
                }
            }
        }

        #endregion

        private RouteEndPoint CreateLogicalEndPointInternal(RouteEndPointAddress endPoint)
        {
            var physicalEndPoint = GetMultiplexPhysicalEndPoint(endPoint);
            var logger = _loggerFactory?.CreateLogger<RouteEndPoint>();
            return new RouteEndPoint(this, physicalEndPoint, endPoint, logger);
        }

        private IPhysicalEndPoint<TAddress> GetMultiplexPhysicalEndPoint(RouteEndPointAddress endPoint)
        {
            var result = _endPointMultiplexer.GetPhysicalEndPoint("end-points/" + endPoint.ToString()); // TODO: This should be configurable
            Assert(result != null);
            return result;
        }

        private sealed class RouteEndPoint : IRouteEndPoint<TAddress>
        {
            // TODO: This should be configurable
            private static readonly TimeSpan _defaultDelay = new TimeSpan(20 * TimeSpan.TicksPerMillisecond);
            private static readonly TimeSpan _maxDelay = new TimeSpan(12000 * TimeSpan.TicksPerMillisecond);

            private readonly RemoteRoutingSystem<TAddress> _endPointManager;
            private readonly IPhysicalEndPoint<TAddress> _physicalEndPoint;
            private readonly ILogger<RouteEndPoint> _logger;

            private volatile CancellationTokenSource _disposalSource;

            private readonly AsyncProducerConsumerQueue<(Message message, DecodedMessage decodedMessage, TAddress remoteAddress)> _rxQueue;
            private readonly ConcurrentDictionary<int, TaskCompletionSource<(Message message, DecodedMessage DecodedMessage, TAddress remoteAddress)>> _responseTable;
            private readonly ConcurrentDictionary<(RouteEndPointAddress remoteEndPoint, TAddress remoteAddress, int seqNum), CancellationTokenSource> _cancellationTable;
            private readonly IAsyncProcess _receiveProcess;

            private int _nextSeqNum;

            public RouteEndPoint(RemoteRoutingSystem<TAddress> endPointManager,
                                   IPhysicalEndPoint<TAddress> physicalEndPoint,
                                   RouteEndPointAddress endPoint,
                                   ILogger<RouteEndPoint> logger)
            {
                _endPointManager = endPointManager;
                _physicalEndPoint = physicalEndPoint;
                EndPoint = endPoint;
                _logger = logger;

                _disposalSource = new CancellationTokenSource();

                _rxQueue = new AsyncProducerConsumerQueue<(Message message, DecodedMessage decodedMessage, TAddress remoteAddress)>();
                _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<(Message message, DecodedMessage DecodedMessage, TAddress remoteAddress)>>();
                _cancellationTable = new ConcurrentDictionary<(RouteEndPointAddress remoteEndPoint, TAddress remoteAddress, int seqNum), CancellationTokenSource>();
                _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);

                // TODO
                endPointManager._endPointMap.MapEndPointAsync(EndPoint, LocalAddress, cancellation: default).GetAwaiter().GetResult();
            }

            #region ILogicalEndPoint

            public TAddress LocalAddress => _endPointManager.LocalAddress;
            public RouteEndPointAddress EndPoint { get; }

            public async ValueTask<IRouteEndPointReceiveResult<TAddress>> ReceiveAsync(CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out _, out var disposal))
                {
                    try
                    {
                        var (message, decodedMessage, remoteAddress) = await _rxQueue.DequeueAsync(cancellation);
                        return new MessageReceiveResult(this, message, decodedMessage, remoteAddress);
                    }
                    catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                    {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
                }
            }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
            async ValueTask<IRouteEndPointReceiveResult> IRouteEndPoint.ReceiveAsync(CancellationToken cancellation)
            {
                return await ReceiveAsync(cancellation);
            }
#endif

            public async ValueTask<RouteMessageHandleResult> SendAsync(
                Message message,
                RouteEndPointAddress remoteEndPoint,
                TAddress remoteAddress,
                CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out _, out var disposal))
                {
                    try
                    {
                        return await SendInternalAsync(message, remoteEndPoint, remoteAddress, cancellation);
                    }
                    catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                    {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
                }
            }

            public async ValueTask<RouteMessageHandleResult> SendAsync(
                Message message,
                RouteEndPointAddress remoteEndPoint,
                CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out _, out var disposal))
                {
                    try
                    {
                        var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, token2: default);
                        var remoteAddresses = await GetAddressesAsync(remoteEndPoint, cancellation);
                        var operations = new List<Task>();
                        var timeout = default(Task);

                        try
                        {
                            RouteMessageHandleResult lastReceivedResult = default;

                            foreach (var remoteAddress in remoteAddresses)
                            {
                                if (timeout != null)
                                {
                                    operations.Remove(timeout);
                                }

                                var sendOperation = SendInternalAsync(message, remoteEndPoint, remoteAddress, cancellation);
                                operations.Add(sendOperation);
                                timeout = Task.Delay(5000); // TODO: This should be configurable
                                operations.Add(timeout);

                                var completedOperation = await Task.WhenAny(operations);

                                if (completedOperation != timeout)
                                {
                                    var result = await (completedOperation as Task<RouteMessageHandleResult>);

                                    if (result.Handled)
                                    {
                                        return result;
                                    }

                                    lastReceivedResult = result;
                                    operations.Remove(completedOperation);
                                }
                            }

                            Assert(timeout != null);
                            operations.Remove(timeout);

                            if (operations.Any())
                            {
                                return await (await Task.WhenAny(operations) as Task<RouteMessageHandleResult>);
                            }

                            return new RouteMessageHandleResult(
                                lastReceivedResult.RouteMessage, handled: false);
                        }
                        finally
                        {
                            if (operations.Count > 1)
                            {
                                cancellationSource.Cancel();
                            }
                        }
                    }
                    catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                    {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
                }
            }

            private async Task<RouteMessageHandleResult> SendInternalAsync(
                Message message,
                RouteEndPointAddress remoteEndPoint,
                TAddress remoteAddress,
                CancellationToken cancellation)
            {
                var responseSource = EncodeRequestMessage(ref message, remoteEndPoint, out var seqNum);
                var responseTask = responseSource.Task;

                void RequestCancellation()
                {
                    RequestCancellationAsync(seqNum, remoteEndPoint, remoteAddress).HandleExceptions(_logger);
                }

                using (cancellation.Register(RequestCancellation))
                {
                    await Task.WhenAll(SendEncodedMessageInternalAsync(message, remoteEndPoint, remoteAddress, cancellation).AsTask(), responseTask);
                }

                var response = await responseTask;
                return new RouteMessageHandleResult(
                    new RouteMessage<IDispatchResult>(response.message),
                    response.decodedMessage.Handled);
            }

            private async ValueTask<IEnumerable<TAddress>> GetAddressesAsync(
                RouteEndPointAddress remoteEndPoint,
                CancellationToken cancellation)
            {
                var delay = _defaultDelay;
                var addresses = await _endPointManager._endPointMap.GetMapsAsync(remoteEndPoint, cancellation);

                while (!addresses.Any())
                {
                    await Task.Delay(delay, cancellation);
                    addresses = await _endPointManager._endPointMap.GetMapsAsync(remoteEndPoint, cancellation);
                    delay = delay + delay;

                    if (delay > _maxDelay)
                        delay = _maxDelay;
                }

                return _endPointManager._endPointScheduler.Schedule(addresses);
            }

            #endregion

            private ValueTask SendEncodedMessageInternalAsync(
                Message encodedMessage,
                RouteEndPointAddress remoteEndPoint,
                TAddress remoteAddress,
                CancellationToken cancellation)
            {
                var physicalEndPoint = _endPointManager.GetMultiplexPhysicalEndPoint(remoteEndPoint);
                return physicalEndPoint.SendAsync(
                    new Transmission<TAddress>(encodedMessage, remoteAddress), cancellation);
            }

            private ValueTask RequestCancellationAsync(
                int corr,
                RouteEndPointAddress remoteEndPoint,
                TAddress remoteAddress)
            {
                var message = new Message();
                EncodeMessage(ref message, new DecodedMessage(
                    MessageType.CancellationRequest,
                    handled: false,
                    GetNextSeqNum(),
                    corr,
                    EndPoint,
                    remoteEndPoint));

                return SendEncodedMessageInternalAsync(message, remoteEndPoint, remoteAddress, cancellation: default);
            }

            #region Receive

            private async Task ReceiveProcess(CancellationToken cancellation)
            {
                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        // Receive a single message
                        var transmission = await _physicalEndPoint.ReceiveAsync(cancellation);

                        Task.Run(() => HandleMessageAsync(transmission.Message, transmission.RemoteAddress, cancellation)).HandleExceptions(_logger);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, $"Failure in receive process for local end-point '{EndPoint}'.");
                    }
                }
            }

            private async Task HandleMessageAsync(Message message, TAddress remoteAddress, CancellationToken cancellation)
            {
                DecodeMessage(ref message, out var decodedMessage);

                if (!EndPoint.Equals(decodedMessage.RxEndPoint))
                {
                    await SendMisroutedAsync(remoteAddress, decodedMessage.RxEndPoint, decodedMessage.SeqNum, cancellation);
                    return;
                }

                switch (decodedMessage.MessageType)
                {
                    case MessageType.Request:
                        await HandleRequestAsync(message, decodedMessage, remoteAddress, cancellation);
                        break;

                    case MessageType.Response:
                        await HandleResponseAsync(message, decodedMessage, remoteAddress, cancellation);
                        break;

                    case MessageType.CancellationRequest:
                        await HandleCancellationRequestAsync(message, decodedMessage, remoteAddress, cancellation);
                        break;

                    case MessageType.CancellationResponse:
                        await HandleCancellationResponseAsync(message, decodedMessage, cancellation);
                        break;

                    case MessageType.EndPointNotPresent:
                    case MessageType.ProtocolNotSupported:
                    case MessageType.Unknown:
                    default:
                        /* TODO */
                        break;
                }
            }

            private ValueTask SendMisroutedAsync(TAddress remoteAddress, RouteEndPointAddress rxEndPoint, int seqNum, CancellationToken cancellation)
            {
                var message = new Message();
                EncodeMessage(ref message, new DecodedMessage(
                    MessageType.Misrouted, handled: false, GetNextSeqNum(), seqNum, EndPoint, rxEndPoint));

                return SendEncodedMessageInternalAsync(message, rxEndPoint, remoteAddress, cancellation);
            }

            private Task HandleRequestAsync(
                Message message,
                DecodedMessage decodedMessage,
                TAddress remoteAddress,
                CancellationToken cancellation)
            {
                _logger?.LogTrace($"Received message from address {remoteAddress}, end-point {decodedMessage.TxEndPoint} for end-point {decodedMessage.RxEndPoint}.");
                _cancellationTable.GetOrAdd((decodedMessage.TxEndPoint, remoteAddress, decodedMessage.SeqNum), _ => new CancellationTokenSource());
                return _rxQueue.EnqueueAsync((message, decodedMessage, remoteAddress), cancellation);
            }

            private Task HandleResponseAsync(
                Message message,
                DecodedMessage decodedMessage,
                TAddress remoteAddress,
                CancellationToken cancellation)
            {
                // We did not already receive a response for this corr-id.
                if (_responseTable.TryGetValue(decodedMessage.Corr, out var responseSource))
                {
                    responseSource.SetResult((message, decodedMessage, remoteAddress));
                }

                return Task.CompletedTask;
            }

            private Task HandleCancellationRequestAsync(
                Message message,
                DecodedMessage decodedMessage,
                TAddress remoteAddress,
                CancellationToken cancellation)
            {
                if (_cancellationTable.TryGetValue(
                    (decodedMessage.TxEndPoint, remoteAddress, decodedMessage.Corr), out var cancellationSource))
                {
                    cancellationSource.Cancel();
                }

                return Task.CompletedTask;
            }

            private Task HandleCancellationResponseAsync(
                Message message,
                DecodedMessage decodedMessage,
                CancellationToken cancellation)
            {
                // We did not already receive a response for this corr-id.
                if (_responseTable.TryGetValue(decodedMessage.Corr, out var responseSource))
                {
                    responseSource.TrySetCanceled();
                }

                return Task.CompletedTask;
            }

            #endregion

            #region Coding

            private readonly struct DecodedMessage // TODO: Rename
            {
                public DecodedMessage(MessageType messageType,
                                      bool handled,
                                      int seqNum,
                                      int corr,
                                      RouteEndPointAddress txEndPoint,
                                      RouteEndPointAddress rxEndPoint)
                {
                    MessageType = messageType;
                    Handled = handled;
                    SeqNum = seqNum;
                    Corr = corr;
                    TxEndPoint = txEndPoint;
                    RxEndPoint = rxEndPoint;
                }
                public MessageType MessageType { get; }
                public bool Handled { get; }
                public int SeqNum { get; }
                public int Corr { get; }
                public RouteEndPointAddress TxEndPoint { get; }
                public RouteEndPointAddress RxEndPoint { get; }
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
                Request = 1,
                Response = 2,
                CancellationRequest = 3,
                CancellationResponse = 4,


                /// <summary>
                /// The protocol of a received message is not supported. The payload is the seq-num of the message in raw format.
                /// </summary>
                ProtocolNotSupported = -1,

                EndPointNotPresent = -2,

                Misrouted = -3
            }

            private void DecodeMessage(ref Message message, out DecodedMessage decodedMessage)
            {
                var builder = message.ToBuilder();

                MessageType messageType;
                bool handled;
                int seqNum, corr;
                RouteEndPointAddress txEndPoint, rxEndPoint;


                using (var frameStream = builder.PopFrame().OpenStream())
                using (var reader = new BinaryReader(frameStream))
                {
                    messageType = (MessageType)reader.ReadInt32();
                    handled = reader.ReadBoolean();
                    reader.ReadInt16();
                    reader.ReadByte();
                    seqNum = reader.ReadInt32();
                    corr = reader.ReadInt32();

                    txEndPoint = reader.ReadEndPointAddress();
                    rxEndPoint = reader.ReadEndPointAddress();
                }

                decodedMessage = new DecodedMessage(messageType, handled, seqNum, corr, txEndPoint, rxEndPoint);
                message = builder.BuildMessage();
            }

            private void EncodeMessage(ref Message message, in DecodedMessage decodedMessage)
            {
                var builder = message.ToBuilder();

                using (var frameStream = builder.PushFrame().OpenStream(overrideContent: true))
                using (var writer = new BinaryWriter(frameStream))
                {
                    writer.Write((int)decodedMessage.MessageType);  // Message type        -- 4 Byte
                    writer.Write(decodedMessage.Handled);
                    writer.Write((short)0);
                    writer.Write((byte)0);
                    writer.Write(decodedMessage.SeqNum);            // Seq num             -- 4 Byte
                    writer.Write(decodedMessage.Corr);              // Coor num            -- 4 Byte
                    writer.Write(decodedMessage.TxEndPoint);
                    writer.Write(decodedMessage.RxEndPoint);
                }

                message = builder.BuildMessage();
            }

            private TaskCompletionSource<(Message message, DecodedMessage decodedMessage, TAddress remoteAddress)> EncodeRequestMessage(
                ref Message message,
                RouteEndPointAddress remoteEndPoint,
                out int seqNum)
            {
                var responseSource = new TaskCompletionSource<(Message message, DecodedMessage decodedMessage, TAddress remoteAddress)>();
                seqNum = AllocateResponseTableSlot(responseSource);

                EncodeMessage(ref message, new DecodedMessage(MessageType.Request, handled: false, seqNum, corr: 0, EndPoint, remoteEndPoint));
                return responseSource;
            }

            #endregion

            #region Disposal

            public ValueTask DisposeAsync()
            {
                Dispose();
                return default;
            }

            public void Dispose()
            {
                var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

                if (disposalSource != null)
                {
                    _logger?.LogDebug($"Unmap local end-point '{EndPoint}' from physical end-point {LocalAddress}.");
                    disposalSource.Cancel();
                    _endPointManager._endPointMap.UnmapEndPointAsync(EndPoint, LocalAddress, cancellation: default).HandleExceptionsAsync(_logger).GetAwaiter().GetResult(); // TODO
                    _receiveProcess.Terminate();
                    _physicalEndPoint.Dispose();

                    lock (_endPointManager._endPointsLock)
                    {
                        _endPointManager._endPoints.Remove(EndPoint, this);
                    }

                    disposalSource.Dispose();
                }
            }

            private IDisposable CheckDisposal(ref CancellationToken cancellation,
                                              out CancellationToken externalCancellation,
                                              out CancellationToken disposal)
            {
                var disposalSource = _disposalSource; // Volatile read op

                if (disposalSource == null)
                    throw new ObjectDisposedException(GetType().FullName);

                externalCancellation = cancellation;
                disposal = disposalSource.Token;

                if (cancellation.CanBeCanceled)
                {
                    var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, disposal);
                    cancellation = combinedCancellationSource.Token;

                    return combinedCancellationSource;
                }
                else
                {
                    cancellation = disposal;

                    return NoOpDisposable.Instance;
                }
            }

            #endregion

            private int GetNextSeqNum()
            {
                return Interlocked.Increment(ref _nextSeqNum);
            }

            private int AllocateResponseTableSlot(
                TaskCompletionSource<(Message message, DecodedMessage decodedMessage, TAddress remoteAddress)> responseSource)
            {
                var seqNum = GetNextSeqNum();

                while (!_responseTable.TryAdd(seqNum, responseSource))
                {
                    seqNum = GetNextSeqNum();
                }

                return seqNum;
            }

            private sealed class MessageReceiveResult : IRouteEndPointReceiveResult<TAddress>
            {
                private readonly RouteEndPoint _logicalEndPoint;
                private readonly CancellationTokenSource _cancellationRequestSource;
                private readonly int _seqNum;

                public MessageReceiveResult(
                    RouteEndPoint logicalEndPoint,
                    in Message message,
                    in DecodedMessage decodedMessage,
                    TAddress remoteAddress)
                {
                    _logicalEndPoint = logicalEndPoint;
                    Message = message;
                    RemoteEndPoint = decodedMessage.TxEndPoint;
                    RemoteAddress = remoteAddress;
                    _seqNum = decodedMessage.SeqNum;
                    _cancellationRequestSource = _logicalEndPoint._cancellationTable.GetOrAdd(
                        (RemoteEndPoint, RemoteAddress, _seqNum),
                        new CancellationTokenSource());
                }

                public CancellationToken Cancellation => _cancellationRequestSource.Token;

                public Message Message { get; }

                public RouteEndPointAddress RemoteEndPoint { get; }

                public TAddress RemoteAddress { get; }

                public void Dispose()
                {
                    _cancellationRequestSource.Dispose();
                    _logicalEndPoint._cancellationTable.Remove((RemoteEndPoint, RemoteAddress, _seqNum), _cancellationRequestSource);
                }

                public ValueTask SendResultAsync(RouteMessageHandleResult result)
                {
                    return SendResponseInternalAsync(result.RouteMessage.Message, result.Handled);
                }

                public ValueTask SendCancellationAsync()
                {
                    var message = new Message();
                    var decodedMessage = new DecodedMessage(
                        MessageType.CancellationResponse,
                        handled: false,
                        _logicalEndPoint.GetNextSeqNum(),
                        _seqNum,
                        _logicalEndPoint.EndPoint,
                        RemoteEndPoint);

                    _logicalEndPoint.EncodeMessage(ref message, decodedMessage);

                    return _logicalEndPoint.SendEncodedMessageInternalAsync(
                        message, RemoteEndPoint, RemoteAddress, Cancellation); // TODO: Can we pass Cancellation here?
                }

                public ValueTask SendAckAsync()
                {
                    return SendResponseInternalAsync(default, handled: false);
                }

                private ValueTask SendResponseInternalAsync(Message response, bool handled)
                {
                    var decodedMessage = new DecodedMessage(
                        MessageType.Response,
                        handled,
                        _logicalEndPoint.GetNextSeqNum(),
                        _seqNum,
                        _logicalEndPoint.EndPoint,
                        RemoteEndPoint);

                    _logicalEndPoint.EncodeMessage(ref response, decodedMessage);

                    return _logicalEndPoint.SendEncodedMessageInternalAsync(
                        response, RemoteEndPoint, RemoteAddress, Cancellation); // TODO: Can we pass Cancellation here?
                }
            }
        }
    }
}
