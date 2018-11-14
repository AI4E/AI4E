using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    // TODO: Retransmission for Request and CancellationRequest messages
    public sealed class EndPointManager<TAddress> : IEndPointManager<TAddress>
    {
        private static readonly byte[] _emptyByteArray = new byte[0];

        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly IEndPointMap<TAddress> _endPointMap;
        private readonly IEndPointScheduler<TAddress> _endPointScheduler;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<EndPointManager<TAddress>> _logger;

        private Dictionary<EndPointAddress, LogicalEndPoint> _endPoints;
        private readonly object _endPointsLock = new object();

        // 0: false, 1: true
        private volatile int _isDisposed = 0;

        public EndPointManager(IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                               IEndPointMap<TAddress> endPointMap,
                               IEndPointScheduler<TAddress> endPointScheduler,
                               IServiceProvider serviceProvider,
                               ILoggerFactory loggerFactory)
        {
            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (endPointMap == null)
                throw new ArgumentNullException(nameof(endPointMap));

            if (endPointScheduler == null)
                throw new ArgumentNullException(nameof(endPointScheduler));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _endPointMultiplexer = endPointMultiplexer;
            _endPointMap = endPointMap;
            _endPointScheduler = endPointScheduler;
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<EndPointManager<TAddress>>();

            _endPoints = new Dictionary<EndPointAddress, LogicalEndPoint>();
        }

        #region IEndPointManager

        public TAddress LocalAddress => _endPointMultiplexer.LocalAddress;

        public ILogicalEndPoint<TAddress> GetLogicalEndPoint(EndPointAddress endPoint)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            lock (_endPointsLock)
            {
                if (_endPoints == null)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                if (_endPoints.TryGetValue(endPoint, out var logicalEndPoint))
                {
                    return logicalEndPoint;
                }

                return null;
            }
        }

        public ILogicalEndPoint<TAddress> CreateLogicalEndPoint(EndPointAddress endPoint)
        {
            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (_isDisposed != 0) // Volatile read op.
                throw new ObjectDisposedException(GetType().FullName);

            // We have to ensure that only a single logical end-point exists for each address at any given time.
            // Event a concurrnet dictionary cannot ensure this => We have to lock.
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
                return logicalEndPoint;
            }
        }

        ILogicalEndPoint IEndPointManager.GetLogicalEndPoint(EndPointAddress endPoint)
        {
            return GetLogicalEndPoint(endPoint);
        }

        ILogicalEndPoint IEndPointManager.CreateLogicalEndPoint(EndPointAddress endPoint)
        {
            return CreateLogicalEndPoint(endPoint);
        }

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

        private LogicalEndPoint CreateLogicalEndPointInternal(EndPointAddress endPoint)
        {
            var physicalEndPoint = GetMultiplexPhysicalEndPoint(endPoint);
            var logger = _loggerFactory?.CreateLogger<LogicalEndPoint>();
            return new LogicalEndPoint(this, physicalEndPoint, endPoint, logger);
        }

        private IPhysicalEndPoint<TAddress> GetMultiplexPhysicalEndPoint(EndPointAddress endPoint)
        {
            var result = _endPointMultiplexer.GetPhysicalEndPoint("end-points/" + endPoint.ToString()); // TODO: This should be configurable
            Assert(result != null);
            return result;
        }

        private sealed class LogicalEndPoint : ILogicalEndPoint<TAddress>
        {
            // TODO: This should be configurable
            private static readonly TimeSpan _defaultDelay = new TimeSpan(20 * TimeSpan.TicksPerMillisecond);
            private static readonly TimeSpan _maxDelay = new TimeSpan(12000 * TimeSpan.TicksPerMillisecond);

            private readonly EndPointManager<TAddress> _endPointManager;
            private readonly IPhysicalEndPoint<TAddress> _physicalEndPoint;
            private readonly ILogger<LogicalEndPoint> _logger;

            private volatile CancellationTokenSource _disposalSource;

            private readonly AsyncProducerConsumerQueue<(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress)> _rxQueue;
            private readonly ConcurrentDictionary<int, TaskCompletionSource<(IMessage message, DecodedMessage DecodedMessage, TAddress remoteAddress)>> _responseTable;
            private readonly ConcurrentDictionary<(EndPointAddress remoteEndPoint, TAddress remoteAddress, int seqNum), CancellationTokenSource> _cancellationTable;
            private readonly IAsyncProcess _receiveProcess;

            private int _nextSeqNum;

            public LogicalEndPoint(EndPointManager<TAddress> endPointManager,
                                   IPhysicalEndPoint<TAddress> physicalEndPoint,
                                   EndPointAddress endPoint,
                                   ILogger<LogicalEndPoint> logger)
            {
                _endPointManager = endPointManager;
                _physicalEndPoint = physicalEndPoint;
                EndPoint = endPoint;
                _logger = logger;

                _disposalSource = new CancellationTokenSource();

                _rxQueue = new AsyncProducerConsumerQueue<(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress)>();
                _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<(IMessage message, DecodedMessage DecodedMessage, TAddress remoteAddress)>>();
                _cancellationTable = new ConcurrentDictionary<(EndPointAddress remoteEndPoint, TAddress remoteAddress, int seqNum), CancellationTokenSource>();
                _receiveProcess = new AsyncProcess(ReceiveProcess, start: true);

                // TODO
                endPointManager._endPointMap.MapEndPointAsync(EndPoint, LocalAddress, cancellation: default).GetAwaiter().GetResult();
            }

            #region ILogicalEndPoint

            public TAddress LocalAddress => _endPointManager.LocalAddress;
            public EndPointAddress EndPoint { get; }

            public async Task<ILogicalEndPointReceiveResult<TAddress>> ReceiveAsync(CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out var externalCancellation, out var disposal))
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

            // TODO: Can we downcast here, without an async state machine? (See also: RequestReplyClientEndPoint)
            async Task<ILogicalEndPointReceiveResult> ILogicalEndPoint.ReceiveAsync(CancellationToken cancellation)
            {
                return await ReceiveAsync(cancellation);
            }

            public async Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out var externalCancellation, out var disposal))
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

            public async Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out var externalCancellation, out var disposal))
                {
                    try
                    {
                        var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, token2: default);
                        var remoteAddresses = await GetAddressesAsync(remoteEndPoint, cancellation);
                        var operations = new List<Task>();
                        var timeout = default(Task);

                        try
                        {
                            foreach (var remoteAddress in remoteAddresses)
                            {
                                var messageCopy = new Message();

                                message.Trim();
                                var messagePayload = message.ToArray();
                                messageCopy.Read(messagePayload);

                                if (timeout != null)
                                {
                                    operations.Remove(timeout);
                                }

                                operations.Add(SendInternalAsync(messageCopy, remoteEndPoint, remoteAddress, cancellation));
                                timeout = Task.Delay(5000); // TODO: This should be configurable
                                operations.Add(timeout);

                                var completedOperation = await Task.WhenAny(operations);

                                if (completedOperation != timeout)
                                {
                                    return await (completedOperation as Task<IMessage>);
                                }
                            }

                            Assert(timeout != null);
                            operations.Remove(timeout);

                            return await (await Task.WhenAny(operations) as Task<IMessage>);
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

            private async Task<IMessage> SendInternalAsync(IMessage message, EndPointAddress remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
            {
                var responseSource = EncodeRequestMessage(message, remoteEndPoint, out var seqNum);
                var response = responseSource.Task;

                void RequestCancellation()
                {
                    RequestCancellationAsync(seqNum, remoteEndPoint, remoteAddress).HandleExceptions(_logger);
                }

                using (cancellation.Register(RequestCancellation))
                {
                    await Task.WhenAll(SendEncodedMessageInternalAsync(message, remoteEndPoint, remoteAddress, cancellation), response);
                }

                return (await response).message;
            }

            private async ValueTask<IEnumerable<TAddress>> GetAddressesAsync(EndPointAddress remoteEndPoint, CancellationToken cancellation)
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

            private Task SendEncodedMessageInternalAsync(IMessage encodedMessage, EndPointAddress remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation)
            {
                var physicalEndPoint = _endPointManager.GetMultiplexPhysicalEndPoint(remoteEndPoint);
                return physicalEndPoint.SendAsync(encodedMessage, remoteAddress, cancellation);
            }

            private Task RequestCancellationAsync(int corr, EndPointAddress remoteEndPoint, TAddress remoteAddress)
            {
                var message = new Message();
                EncodeMessage(message, new DecodedMessage(MessageType.CancellationRequest, GetNextSeqNum(), corr, EndPoint, remoteEndPoint));

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
                        var (message, remoteAddress) = await _physicalEndPoint.ReceiveAsync(cancellation);

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
                DecodeMessage(message, out var decodedMessage);

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

            private Task SendMisroutedAsync(TAddress remoteAddress, EndPointAddress rxEndPoint, int seqNum, CancellationToken cancellation)
            {
                var message = new Message();
                EncodeMessage(message, new DecodedMessage(MessageType.Misrouted, GetNextSeqNum(), seqNum, EndPoint, rxEndPoint));

                return SendEncodedMessageInternalAsync(message, rxEndPoint, remoteAddress, cancellation);
            }

            private Task HandleRequestAsync(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress, CancellationToken cancellation)
            {
                _logger?.LogTrace($"Received message from address {remoteAddress}, end-point {decodedMessage.TxEndPoint} for end-point {decodedMessage.RxEndPoint}.");
                _cancellationTable.GetOrAdd((decodedMessage.TxEndPoint, remoteAddress, decodedMessage.SeqNum), _ => new CancellationTokenSource());
                return _rxQueue.EnqueueAsync((message, decodedMessage, remoteAddress), cancellation);
            }

            private Task HandleResponseAsync(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress, CancellationToken cancellation)
            {
                // We did not already receive a response for this corr-id.
                if (_responseTable.TryGetValue(decodedMessage.Corr, out var responseSource))
                {
                    responseSource.SetResult((message, decodedMessage, remoteAddress));
                }

                return Task.CompletedTask;
            }

            private Task HandleCancellationRequestAsync(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress, CancellationToken cancellation)
            {
                if (_cancellationTable.TryGetValue((decodedMessage.TxEndPoint, remoteAddress, decodedMessage.Corr), out var cancellationSource))
                {
                    cancellationSource.Cancel();
                }

                return Task.CompletedTask;
            }

            private Task HandleCancellationResponseAsync(IMessage message, DecodedMessage decodedMessage, CancellationToken cancellation)
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
                                      int seqNum,
                                      int corr,
                                      EndPointAddress txEndPoint,
                                      EndPointAddress rxEndPoint)
                {
                    MessageType = messageType;
                    SeqNum = seqNum;
                    Corr = corr;
                    TxEndPoint = txEndPoint;
                    RxEndPoint = rxEndPoint;
                }
                public MessageType MessageType { get; }
                public int SeqNum { get; }
                public int Corr { get; }
                public EndPointAddress TxEndPoint { get; }
                public EndPointAddress RxEndPoint { get; }
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

            private void DecodeMessage(IMessage message, out DecodedMessage decodedMessage)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                var frameIdx = message.FrameIndex;
                MessageType messageType;
                int seqNum, corr;
                EndPointAddress txEndPoint, rxEndPoint;

                try
                {
                    using (var frameStream = message.PopFrame().OpenStream())
                    using (var reader = new BinaryReader(frameStream))
                    {
                        messageType = (MessageType)reader.ReadInt32();
                        seqNum = reader.ReadInt32();
                        corr = reader.ReadInt32();

                        txEndPoint = reader.ReadEndPointAddress();
                        rxEndPoint = reader.ReadEndPointAddress();
                    }
                }
                catch when (message.FrameIndex != frameIdx)
                {
                    message.PushFrame();
                    Assert(message.FrameIndex == frameIdx);
                    throw;
                }

                decodedMessage = new DecodedMessage(messageType, seqNum, corr, txEndPoint, rxEndPoint);
            }

            private void EncodeMessage(IMessage message, in DecodedMessage decodedMessage)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                var frameIdx = message.FrameIndex;

                try
                {
                    using (var frameStream = message.PushFrame().OpenStream(overrideContent: true))
                    using (var writer = new BinaryWriter(frameStream))
                    {
                        writer.Write((int)decodedMessage.MessageType);  // Message type        -- 4 Byte
                        writer.Write(decodedMessage.SeqNum);            // Seq num             -- 4 Byte
                        writer.Write(decodedMessage.Corr);              // Coor num            -- 4 Byte
                        writer.Write(decodedMessage.TxEndPoint);
                        writer.Write(decodedMessage.RxEndPoint);
                    }
                }
                catch when (message.FrameIndex != frameIdx)
                {
                    message.PopFrame();
                    Assert(message.FrameIndex == frameIdx);
                    throw;
                }
            }

            private TaskCompletionSource<(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress)> EncodeRequestMessage(
                IMessage message,
                EndPointAddress remoteEndPoint,
                out int seqNum)
            {
                var responseSource = new TaskCompletionSource<(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress)>();
                seqNum = AllocateResponseTableSlot(responseSource);

                EncodeMessage(message, new DecodedMessage(MessageType.Request, seqNum, corr: 0, EndPoint, remoteEndPoint));
                return responseSource;
            }

            #endregion

            #region Disposal

            public void Dispose()
            {
                var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

                if (disposalSource != null)
                {
                    _logger?.LogDebug($"Unmap local end-point '{EndPoint}' from physical end-point {LocalAddress}.");
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

            private int AllocateResponseTableSlot(TaskCompletionSource<(IMessage message, DecodedMessage decodedMessage, TAddress remoteAddress)> responseSource)
            {
                var seqNum = GetNextSeqNum();

                while (!_responseTable.TryAdd(seqNum, responseSource))
                {
                    seqNum = GetNextSeqNum();
                }

                return seqNum;
            }

            private sealed class MessageReceiveResult : ILogicalEndPointReceiveResult<TAddress>
            {
                private readonly LogicalEndPoint _logicalEndPoint;
                private readonly CancellationTokenSource _cancellationRequestSource;
                private readonly int _seqNum;

                public MessageReceiveResult(LogicalEndPoint logicalEndPoint, IMessage message, in DecodedMessage decodedMessage, TAddress remoteAddress)
                {
                    _logicalEndPoint = logicalEndPoint;
                    Message = message;
                    RemoteEndPoint = decodedMessage.TxEndPoint;
                    RemoteAddress = remoteAddress;
                    _seqNum = decodedMessage.SeqNum;

                    _cancellationRequestSource = _logicalEndPoint._cancellationTable.GetOrAdd((RemoteEndPoint, RemoteAddress, _seqNum), new CancellationTokenSource());
                }

                public CancellationToken Cancellation => _cancellationRequestSource.Token;

                public IMessage Message { get; }

                public EndPointAddress RemoteEndPoint { get; }

                public TAddress RemoteAddress { get; }

                Packet<EndPointAddress> IMessageReceiveResult<Packet<EndPointAddress>>.Packet
                    => new Packet<EndPointAddress>(Message, RemoteEndPoint);

                Packet<EndPointAddress, TAddress> IMessageReceiveResult<Packet<EndPointAddress, TAddress>>.Packet
                    => new Packet<EndPointAddress, TAddress>(Message, RemoteEndPoint, RemoteAddress);

                public void Dispose()
                {
                    _cancellationRequestSource.Dispose();
                    _logicalEndPoint._cancellationTable.Remove((RemoteEndPoint, RemoteAddress, _seqNum), _cancellationRequestSource);
                }

                public Task SendResponseAsync(IMessage response)
                {
                    if (response == null)
                        throw new ArgumentNullException(nameof(response));

                    return SendResponseInternalAsync(response);
                }

                public Task SendCancellationAsync()
                {
                    var message = new Message();
                    _logicalEndPoint.EncodeMessage(message, new DecodedMessage(MessageType.CancellationResponse, _logicalEndPoint.GetNextSeqNum(), _seqNum, _logicalEndPoint.EndPoint, RemoteEndPoint));

                    return _logicalEndPoint.SendEncodedMessageInternalAsync(message, RemoteEndPoint, RemoteAddress, Cancellation); // TODO: Can we pass Cancellation here?
                }

                public Task SendAckAsync()
                {
                    return SendResponseInternalAsync(null);
                }

                private Task SendResponseInternalAsync(IMessage response)
                {
                    if (response == null)
                    {
                        response = new Message();
                    }

                    _logicalEndPoint.EncodeMessage(response, new DecodedMessage(MessageType.Response, _logicalEndPoint.GetNextSeqNum(), _seqNum, _logicalEndPoint.EndPoint, RemoteEndPoint));

                    return _logicalEndPoint.SendEncodedMessageInternalAsync(response, RemoteEndPoint, RemoteAddress, Cancellation); // TODO: Can we pass Cancellation here?
                }
            }
        }
    }
}
