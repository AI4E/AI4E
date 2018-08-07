using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    // TODO: (1) Logging
    //       (2) Are dead clients removed from the client lookup table?
    //       (3) If a message is sent from a caller, the message may by put to the underlying end-point, just in the time that the signalr connection is already re-established. 
    //           The logical end-point now tries to send a message to a client address that is not available any more, or more dangerous is already allocated for a completely other client.
    //       (4) There are lots of duplicates, both in the type itself and compared to LogicalClientEndPoint. Maybe a common base class can help here.
    public sealed class LogicalServerEndPoint : ILogicalServerEndPoint, IAsyncDisposable
    {
        #region Fields

        private readonly IServerEndPoint _endPoint;
        private readonly IConnectedClientLookup _connectedClients;
        private readonly ILogger<LogicalServerEndPoint> _logger;

        private readonly AsyncProducerConsumerQueue<(IMessage message, int seqNum, EndPointRoute endPoint)> _rxQueue = new AsyncProducerConsumerQueue<(IMessage message, int seqNum, EndPointRoute endPoint)>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<IMessage>> _responseTable = new ConcurrentDictionary<int, TaskCompletionSource<IMessage>>();
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationTable = new ConcurrentDictionary<int, CancellationTokenSource>();
        private readonly ConcurrentDictionary<EndPointRoute, string> _clientLookup = new ConcurrentDictionary<EndPointRoute, string>();

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        private int _nextSeqNum;

        #endregion

        #region C'tor

        public LogicalServerEndPoint(IServerEndPoint endPoint, IConnectedClientLookup connectedClients, ILogger<LogicalServerEndPoint> logger = null)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            if (connectedClients == null)
                throw new ArgumentNullException(nameof(connectedClients));

            _endPoint = endPoint;
            _connectedClients = connectedClients;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region ILogicalServerEndPoint

        public async Task<IMessage> SendAsync(IMessage message, EndPointRoute endPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    await _initializationHelper.Initialization.WithCancellation(combinedCancellation);

                    var seqNum = GetNextSeqNum();
                    var responseSource = new TaskCompletionSource<IMessage>();

                    while (!_responseTable.TryAdd(seqNum, responseSource))
                    {
                        seqNum = GetNextSeqNum();
                    }

                    void RequestCancellation()
                    {
                        var cancellationRequest = new Message();
                        EncodeServerMessage(cancellationRequest, GetNextSeqNum(), corr: seqNum, MessageType.CancellationRequest);
                        SendInternalAsync(cancellationRequest, endPoint, cancellation: default).HandleExceptions(_logger);
                    }

                    EncodeServerMessage(message, seqNum, corr: default, MessageType.Request);

                    using (combinedCancellation.Register(RequestCancellation))
                    {
                        await Task.WhenAll(SendInternalAsync(message, endPoint, combinedCancellation), responseSource.Task);
                    }

                    return await responseSource.Task;
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task ReceiveAsync(Func<IMessage, EndPointRoute, CancellationToken, Task<IMessage>> handler, CancellationToken cancellation)
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
                    await _initializationHelper.Initialization.WithCancellation(combinedCancellation);

                    var (message, seqNum, endPoint) = await _rxQueue.DequeueAsync(combinedCancellation);


                    var cancellationRequestSource = _cancellationTable.GetOrAdd(seqNum, new CancellationTokenSource());
                    var cancellationRequest = cancellationRequestSource.Token;

                    IMessage response;

                    try
                    {
                        response = await handler(message, endPoint, CancellationTokenSource.CreateLinkedTokenSource(combinedCancellation, cancellationRequest).Token);
                    }
                    catch (OperationCanceledException) when (cancellationRequest.IsCancellationRequested)
                    {
                        var cancellationResponse = new Message();

                        EncodeServerMessage(cancellationResponse, seqNum: GetNextSeqNum(), corr: seqNum, MessageType.CancellationResponse);

                        await SendInternalAsync(cancellationResponse, endPoint, combinedCancellation);

                        return;
                    }

                    if (response == null)
                    {
                        response = new Message();
                    }

                    EncodeServerMessage(response, seqNum: GetNextSeqNum(), corr: seqNum, MessageType.Response);

                    await SendInternalAsync(response, endPoint, combinedCancellation);
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        #region Encode/Decode

        // These are the counterparts of the encode/decode functions in LogicalClientEndPoint. These must be in sync.
        // TODO: Create a base class and move the functions there.

        private static void EncodeServerMessage(IMessage message, int seqNum, int corr, MessageType messageType)
        {
            using (var stream = message.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(seqNum); // 4 bytes
                writer.Write((byte)messageType); // 1 bytes
                writer.Write((byte)0); // 1 bytes (padding)
                writer.Write((byte)0); // 1 bytes (padding)
                writer.Write((byte)0); // 1 bytes (padding)
                writer.Write(corr); // 4 bytes
            }
        }

        private static (int seqNum, int corr, MessageType messageType, EndPointRoute remoteEndPoint, string securityToken) DecodeClientMessage(IMessage message)
        {
            EndPointRoute remoteEndPoint = null;
            string securityToken = null;

            using (var stream = message.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                var seqNum = reader.ReadInt32(); // 4 bytes
                var messageType = (MessageType)reader.ReadByte(); // 1 bytes
                reader.ReadByte(); // 1 bytes (padding)
                reader.ReadByte(); // 1 bytes (padding)
                reader.ReadByte(); // 1 bytes (padding)
                var corr = reader.ReadInt32(); // 4 bytes

                var remoteEndPointBytesLength = reader.ReadInt32(); // 4 bytes

                if (remoteEndPointBytesLength > 0)
                {
                    var remoteEndPointBytes = reader.ReadBytes(remoteEndPointBytesLength); // Variable length
                    remoteEndPoint = EndPointRoute.CreateRoute(Encoding.UTF8.GetString(remoteEndPointBytes));
                }

                var securityTokenBytesLength = reader.ReadInt32(); // 4 bytes

                if (securityTokenBytesLength > 0)
                {
                    var securityTokenBytes = reader.ReadBytes(securityTokenBytesLength); // Variable length
                    securityToken = Encoding.UTF8.GetString(securityTokenBytes);
                }

                return (seqNum, corr, messageType, remoteEndPoint, securityToken);
            }
        }

        // This must be in sync with LogicalClientEndPoint.DecodeInitResponse
        private static void EncodeInitResponse(IMessage result, EndPointRoute endPoint, string securityToken)
        {
            var endPointBytes = Encoding.UTF8.GetBytes(endPoint.Route);
            var securityTokenBytes = Encoding.UTF8.GetBytes(securityToken);

            using (var stream = result.PushFrame().OpenStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(endPointBytes.Length);
                    writer.Write(endPointBytes);

                    writer.Write(securityTokenBytes.Length);
                    writer.Write(securityTokenBytes);
                }
            }
        }

        #endregion

        #region Send

        // The message is encoded (Our message frame is on tos)
        private Task SendInternalAsync(IMessage message, string address, CancellationToken cancellation)
        {
            return _endPoint.SendAsync(message, address, cancellation);
        }

        // The message is encoded (Our message frame is on tos)
        private Task SendInternalAsync(IMessage message, EndPointRoute endPoint, CancellationToken cancellation)
        {
            var address = LookupAddress(endPoint);

            if (address == null)
            {
                throw new Exception($"The client '{endPoint.Route}' is unreachable."); // TODO
            }

            return SendInternalAsync(message, address, cancellation);
        }

        private string LookupAddress(EndPointRoute endPoint)
        {
            if (_clientLookup.TryGetValue(endPoint, out var address))
            {
                return address;
            }

            return null;
        }

        #endregion

        #region Receive

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, address) = await _endPoint.ReceiveAsync(cancellation);
                    Task.Run(() => ReceiveInternalAsync(message, address, cancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log
                }
            }
        }

        // The message is encoded (Our message frame is on tos)
        private async Task ReceiveInternalAsync(IMessage message, string address, CancellationToken cancellation)
        {
            var (seqNum, corr, messageType, remoteEndPoint, securityToken) = DecodeClientMessage(message);

            if (messageType == MessageType.Init)
            {
                await ReceiveInitAsync(message, seqNum, corr, address, cancellation);
                return;
            }

            if (!await ValidateIntegrityAsync(address, remoteEndPoint, securityToken, cancellation))
            {
                // TODO: Send bad client response
                // TODO: Log bad request

                return;
            }

            switch (messageType)
            {
                case MessageType.Request:
                    await ReceiveRequestAsync(message, seqNum, remoteEndPoint, cancellation);
                    break;

                case MessageType.Response:
                    await ReceiveResponseAsync(message, seqNum, corr, cancellation);
                    break;

                case MessageType.CancellationRequest:
                    await ReceiveCancellationRequestAsync(message, seqNum, corr, cancellation);
                    break;

                case MessageType.CancellationResponse:
                    await ReceiveCancellationResponseAsnyc(message, seqNum, corr, cancellation);
                    break;

                default:
                    // Unknown message type. TODO: Log
                    break;
            }
        }

        private async Task<bool> ValidateIntegrityAsync(string address, EndPointRoute remoteEndPoint, string securityToken, CancellationToken cancellation)
        {
            var result = await _connectedClients.ValidateClientAsync(remoteEndPoint, securityToken, cancellation);

            if (result)
            {
                _clientLookup.AddOrUpdate(remoteEndPoint, address, (_, entry) => address);
            }

            return result;
        }

        private async Task ReceiveInitAsync(IMessage message, int seqNum, int corr, string address, CancellationToken cancellation)
        {
            var cancellationRequestSource = _cancellationTable.GetOrAdd(seqNum, _ => new CancellationTokenSource());
            var cancellationRequest = cancellationRequestSource.Token;

            var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationRequest, cancellation).Token;

            EndPointRoute endPoint;
            string securityToken;

            try
            {

                (endPoint, securityToken) = await _connectedClients.AddClientAsync(combinedCancellation);

                var sucess = _clientLookup.TryAdd(endPoint, address);
                Assert(sucess);
            }
            catch (OperationCanceledException) when (cancellationRequest.IsCancellationRequested)
            {
                var cancellationResponse = new Message();

                EncodeServerMessage(cancellationResponse, seqNum: GetNextSeqNum(), corr: seqNum, MessageType.CancellationResponse);

                await SendInternalAsync(cancellationResponse, address, combinedCancellation);

                return;
            }

            var initResponse = new Message();

            EncodeInitResponse(initResponse, endPoint, securityToken);
            EncodeServerMessage(initResponse, GetNextSeqNum(), corr: seqNum, MessageType.Response);

            await SendInternalAsync(initResponse, address, combinedCancellation);
        }

        private Task ReceiveRequestAsync(IMessage message, int seqNum, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            _cancellationTable.GetOrAdd(seqNum, _ => new CancellationTokenSource());

            return _rxQueue.EnqueueAsync((message, seqNum, remoteEndPoint), cancellation);
        }

        private Task ReceiveResponseAsync(IMessage message, int seqNum, int corr, CancellationToken cancellation)
        {
            // We did not already receive a response for this corr-id.
            if (!_responseTable.TryGetValue(corr, out var responseSource))
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

        private Task InitializeInternalAsync(CancellationToken cancellation)
        {
            return _receiveProcess.StartAsync(cancellation);
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

        // TODO: This is a duplicate from LogicalClientEndPoint
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
