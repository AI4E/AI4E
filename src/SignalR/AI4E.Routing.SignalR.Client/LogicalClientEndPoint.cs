using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Routing.SignalR.Client
{
    // TODO: If a message is received twice, due to a reconnection, 
    //       the second reception received a slot in the cancellation table 
    //       but the remote end may already have received the cancellation or response of the first message.
    public sealed class LogicalClientEndPoint : ILogicalClientEndPoint, IAsyncDisposable
    {
        #region Fields

        private static readonly byte[] _emptyBytes = new byte[0];

        private readonly IClientEndPoint _endPoint;
        private readonly ILogger<LogicalClientEndPoint> _logger;

        private readonly AsyncProducerConsumerQueue<(IMessage message, int seqNum)> _rxQueue = new AsyncProducerConsumerQueue<(IMessage message, int seqNum)>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<IMessage>> _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<IMessage>>();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationTable = new ConcurrentDictionary<int, CancellationTokenSource>();

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper<(EndPointRoute localEndPoint, string securityToken)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        private int _nextSeqNum;

        #endregion

        #region C'tor

        public LogicalClientEndPoint(IClientEndPoint endPoint, ILogger<LogicalClientEndPoint> logger = null)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            _endPoint = endPoint;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper<(EndPointRoute localEndPoint, string securityToken)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region ILogicalClientEndPoint

        public async Task<IMessage> SendAsync(IMessage message, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var (localEndPoint, securityToken) = await _initializationHelper.Initialization.WithCancellation(combinedCancellation);

                    var seqNum = GetNextSeqNum();
                    var responseSource = new TaskCompletionSource<IMessage>();

                    while (!_responseTable.TryAdd(seqNum, responseSource))
                    {
                        seqNum = GetNextSeqNum();
                    }

                    void RequestCancellation()
                    {
                        var cancellationRequest = new Message();
                        EncodeClientMessage(cancellationRequest, GetNextSeqNum(), corr: seqNum, MessageType.CancellationRequest, localEndPoint, securityToken);
                        SendInternalAsync(cancellationRequest, cancellation: default).HandleExceptions(_logger);
                    }

                    EncodeClientMessage(message, seqNum, corr: default, MessageType.Request, localEndPoint, securityToken);

                    using (combinedCancellation.Register(RequestCancellation))
                    {
                        await Task.WhenAll(SendInternalAsync(message, combinedCancellation), responseSource.Task);
                    }

                    return await responseSource.Task;
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task ReceiveAsync(Func<IMessage, CancellationToken, Task<IMessage>> handler, CancellationToken cancellation)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var (localEndPoint, securityToken) = await _initializationHelper.Initialization.WithCancellation(combinedCancellation);

                    var (message, seqNum) = await _rxQueue.DequeueAsync(combinedCancellation);

                    var cancellationRequestSource = _cancellationTable.GetOrAdd(seqNum, new CancellationTokenSource());
                    var cancellationRequest = cancellationRequestSource.Token;

                    IMessage response;

                    try
                    {
                        response = await handler(message, CancellationTokenSource.CreateLinkedTokenSource(combinedCancellation, cancellationRequest).Token);
                    }
                    catch (OperationCanceledException) when (cancellationRequest.IsCancellationRequested)
                    {
                        var cancellationResponse = new Message();

                        EncodeClientMessage(cancellationResponse, seqNum: GetNextSeqNum(), corr: seqNum, MessageType.CancellationResponse, localEndPoint, securityToken);

                        await SendInternalAsync(cancellationResponse, combinedCancellation);

                        return;
                    }

                    if (response == null)
                    {
                        response = new Message();
                    }

                    EncodeClientMessage(response, seqNum: GetNextSeqNum(), corr: seqNum, MessageType.Response, localEndPoint, securityToken);

                    await SendInternalAsync(response, combinedCancellation);
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async ValueTask<EndPointRoute> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var (localEndPoint, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);

                    return localEndPoint;
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        #region Encode/Decode

        // These are the counterparts of the encode/decode functions in LogicalServerEndPoint. These must be in sync.
        // TODO: Create a base class and move the functions there.

        private static void EncodeClientMessage(IMessage message, int seqNum, int corr, MessageType messageType, EndPointRoute localEndPoint, string securityToken)
        {
            var endPointBytes = localEndPoint == null ? _emptyBytes : Encoding.UTF8.GetBytes(localEndPoint.Route);
            var securityTokenBytes = securityToken == null ? _emptyBytes : Encoding.UTF8.GetBytes(securityToken);

            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(seqNum); // 4 bytes
                writer.Write((byte)messageType); // 1 bytes
                writer.Write((byte)0); // 1 bytes (padding)
                writer.Write((byte)0); // 1 bytes (padding)
                writer.Write((byte)0); // 1 bytes (padding)
                writer.Write(corr); // 4 bytes
                writer.Write(endPointBytes.Length); // 4 bytes
                if (endPointBytes.Length > 0)
                {
                    writer.Write(endPointBytes); // Variable length
                }
                writer.Write(securityTokenBytes.Length); // 4 bytes
                if (securityTokenBytes.Length > 0)
                {
                    writer.Write(securityTokenBytes); // Variable length
                }
            }
        }

        private static (int seqNum, int corr, MessageType messageType) DecodeServerMessage(IMessage message)
        {
            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var seqNum = reader.ReadInt32(); // 4 bytes
                var messageType = (MessageType)reader.ReadByte(); // 1 bytes
                reader.ReadByte(); // 1 bytes (padding)
                reader.ReadByte(); // 1 bytes (padding)
                reader.ReadByte(); // 1 bytes (padding)
                var corr = reader.ReadInt32(); // 4 bytes

                return (seqNum, corr, messageType);
            }
        }

        // This must be in sync with LogicalServerEndPoint.EncodeInitResponse
        private static (EndPointRoute localEndPoint, string securityToken) DecodeInitResponse(IMessage result)
        {
            using (var stream = result.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var localEndPointBytesLenght = reader.ReadInt32();
                var localEndPointBytes = reader.ReadBytes(localEndPointBytesLenght);
                var localEndPoint = Encoding.UTF8.GetString(localEndPointBytes);

                var securityTokenBytesLength = reader.ReadInt32();
                var securityTokenBytes = reader.ReadBytes(securityTokenBytesLength);
                var securityToken = Encoding.UTF8.GetString(securityTokenBytes);

                return (EndPointRoute.CreateRoute(localEndPoint), securityToken);
            }
        }

        #endregion

        #region Send

        // The message is encoded (Our message frame is on tos)
        private Task SendInternalAsync(IMessage message, CancellationToken cancellation)
        {
            return _endPoint.SendAsync(message, cancellation);
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = await _endPoint.ReceiveAsync(cancellation);
                    Task.Run(() => ReceiveInternalAsync(message, cancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        // The message is encoded (Our message frame is on tos)
        private Task ReceiveInternalAsync(IMessage message, CancellationToken cancellation)
        {
            var (seqNum, corr, messageType) = DecodeServerMessage(message);

            if (messageType == MessageType.Request)
            {
                return ReceiveRequestAsync(message, seqNum, cancellation);
            }

            if (messageType == MessageType.Response)
            {
                return ReceiveResponseAsync(message, seqNum, corr, cancellation);
            }

            if (messageType == MessageType.CancellationRequest)
            {
                return ReceiveCancellationRequestAsync(message, seqNum, corr, cancellation);
            }

            if (messageType == MessageType.CancellationResponse)
            {
                return ReceiveCancellationResponseAsnyc(message, seqNum, corr, cancellation);
            }

            // Unknown message type. TODO: Log
            return Task.CompletedTask;
        }

        private Task ReceiveRequestAsync(IMessage message, int seqNum, CancellationToken cancellation)
        {
            _cancellationTable.GetOrAdd(seqNum, _ => new CancellationTokenSource());

            return _rxQueue.EnqueueAsync((message, seqNum), cancellation);
        }

        private Task ReceiveResponseAsync(IMessage message, int seqNum, int corr, CancellationToken cancellation)
        {
            // We did not already receive a response for this corr-id.
            if (_responseTable.TryGetValue(corr, out var responseSource))
            {
                responseSource.SetResult(message);
            }

            return Task.CompletedTask;
        }

        private Task ReceiveCancellationRequestAsync(IMessage message, int seqNum, int corr, CancellationToken cancellation)
        {
            if (_cancellationTable.TryGetValue(corr, out var cancellationSource))
            {
                cancellationSource.Cancel();
            }

            return Task.CompletedTask;
        }

        private Task ReceiveCancellationResponseAsnyc(IMessage message, int seqNum, int corr, CancellationToken cancellation)
        {
            // We did not already receive a response for this corr-id.
            if (_responseTable.TryGetValue(corr, out var responseSource))
            {
                responseSource.TrySetCanceled();
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Init

        private async Task<(EndPointRoute localEndPoint, string securityToken)> InitializeInternalAsync(CancellationToken cancellation)
        {
            await _receiveProcess.StartAsync(cancellation);

            var message = new Message();
            var seqNum = GetNextSeqNum();
            var responseSource = new TaskCompletionSource<IMessage>();

            while (!_responseTable.TryAdd(seqNum, responseSource))
            {
                seqNum = GetNextSeqNum();
            }

            void RequestCancellation()
            {
                var cancellationRequest = new Message();
                EncodeClientMessage(cancellationRequest, GetNextSeqNum(), corr: seqNum, MessageType.CancellationRequest, localEndPoint: null, securityToken: null);
                SendInternalAsync(cancellationRequest, cancellation: default).HandleExceptions(_logger);
            }

            EncodeClientMessage(message, seqNum, corr: default, MessageType.Init, localEndPoint: null, securityToken: null);

            using (cancellation.Register(RequestCancellation))
            {
                await Task.WhenAll(SendInternalAsync(message, cancellation), responseSource.Task);
            }

            var result = await responseSource.Task;
            return DecodeInitResponse(result);
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
            await _initializationHelper.CancelAsync().HandleExceptionsAsync();
            await _receiveProcess.TerminateAsync().HandleExceptionsAsync();
        }

        #endregion

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        // TODO: This is a duplicate from LogicalServerEndPoint
        private enum MessageType : byte
        {
            Init = 0,
            Request = 1,
            Response = 2,
            CancellationRequest = 3,
            CancellationResponse = 4
        }
    }
}
