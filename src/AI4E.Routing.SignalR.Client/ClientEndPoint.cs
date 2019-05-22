using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Remoting;
using AI4E.Routing.SignalR.Server;
using AI4E.Utils;
using AI4E.Utils.Memory;
using AI4E.Utils.Processing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Routing.SignalR.Client
{
    public sealed partial class ClientEndPoint : IClientEndPoint
    {
        #region Fields

        private readonly HubConnection _hubConnection;
        private readonly ILogger<ClientEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue;
        private readonly ConcurrentDictionary<int, (ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)> _txQueue;

        private readonly ClientCallStub _client;
        private readonly IDisposable _stubRegistration;
        private readonly ReconnectionManager _reconnectionManager;
        private volatile CancellationTokenSource _disposalSource = new CancellationTokenSource();

        private int _nextSeqNum;

        private string _address, _endPoint, _securityToken;


        private readonly TaskCompletionSource<EndPointAddress> _localEndPointTaskSource = new TaskCompletionSource<EndPointAddress>();

        private TimeSpan _timeout;
        private DateTime _lastSendOperation;
        private readonly object _lastSendOperationLock = new object();
        private readonly AsyncProcess _keepAliveProcess;

        #endregion

        #region C'tor

        public ClientEndPoint(HubConnection hubConnection, ILogger<ClientEndPoint> logger = null)
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            _hubConnection = hubConnection;
            _logger = logger;
            _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
            _txQueue = new ConcurrentDictionary<int, (ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)>();
            _hubConnection.Closed += UnderlyingConnectionLostAsync;

            _client = new ClientCallStub(this);
            _stubRegistration = _hubConnection.Register(_client);

            _reconnectionManager = new ReconnectionManager(this);

            // The process is started when the connection is established.
            // Create the keep-alive process before the first connection attempt, as the reconnection uses this.
            _keepAliveProcess = new AsyncProcess(KeepAliveProcess, start: false);

            // Intitially, we are unconnected and have to connect the fist time.
            _reconnectionManager.Reconnect();
        }

        #endregion

        #region IClientEndPoint

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<EndPointAddress>(_localEndPointTaskSource.Task.WithCancellation(cancellation));
        }

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var externalCancellation, out var disposal))
            {
                try
                {
                    return await _rxQueue.DequeueAsync(cancellation);
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task SendAsync(IMessage message, CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var externalCancellation, out var disposal))
            {
                try
                {
                    using (ArrayPool<byte>.Shared.RentExact((int)message.Length, out var memory))
                    {
                        message.Write(memory.Span);

                        await SendAsync(memory, cancellation);
                    }
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        private async Task SendAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellation)
        {
            var ackSource = new TaskCompletionSource<object>();
            var seqNum = GetNextSeqNum();

            while (!_txQueue.TryAdd(seqNum, (memory, ackSource)))
            {
                seqNum = GetNextSeqNum();
            }

            // Try to send the message. If we are unconnected currently or our connection breaks in the meantime,
            // do not execute or cancel the send respectively. The message is already put to the tx-queue.
            // The reconnection process will (re)send the message after the connection is (re)established.

            try
            {
                // We cannot assume that the operation is truly cancelled. 
                // It is possible that the cancellation is invoked, when the message is just acked,
                // but before the delegate is unregistered from the cancellation token.

                _logger?.LogDebug($"Sending message ({memory.Length} total bytes) with seq-num {seqNum}.");

                var connectionLostToken = _reconnectionManager.ConnectionLost;

                //  Cancel the send, if the collection is lost in the meantime, as is done in the ordinary send operation.
                if (!connectionLostToken.IsConnectionLost)
                {
                    using var cancellationTokenSource = new TaskCancellationTokenSource(connectionLostToken.AsTask(), cancellation);

                    try
                    {
                        await PushToServerAsync(seqNum, memory, cancellationTokenSource.CancellationToken);
                        SetLastSendOperation();
                    }
                    catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                    {
                        // The connection is broken. The message will be re-sent, when reconnected.
                    }
                }

                await ackSource.Task.WithCancellation(cancellation);
            }
            catch (Exception exc)
            {
                // The operation was either cancellation from outside or the object is disposed or something is wrong.
                if (_txQueue.TryRemove(seqNum, out _))
                {
                    ackSource.TrySetExceptionOrCanceled(exc);
                }

                throw;
            }
        }

        #endregion

        #region Reconnection

        private Task UnderlyingConnectionLostAsync(Exception exception)
        {
            // TODO: Log exception?
            return _reconnectionManager.ReconnectAsync(cancellation: default).AsTask();
        }

        private async ValueTask OnConnectionEstablished(CancellationToken cancellation)
        {
            await _keepAliveProcess.StartAsync(cancellation);

            var connectionLostToken = _reconnectionManager.ConnectionLost;

            // Cancel the retransmission, if the collection is lost in the meantime, as is done in the ordinary send operation.
            if (!connectionLostToken.IsConnectionLost)
            {
                using var cancellationTokenSource = new TaskCancellationTokenSource(connectionLostToken.AsTask(), cancellation);

                // Resend all messages
                await Task.WhenAll(_txQueue.ToList().Select(p => PushToServerAsync(seqNum: p.Key, payload: p.Value.bytes, cancellation: cancellationTokenSource.CancellationToken)));
                SetLastSendOperation();
            }
        }

        private async ValueTask OnConnectionEstablishing(CancellationToken cancellation)
        {
            _logger?.LogDebug("Trying to (re)connect to server.");

            await _keepAliveProcess.TerminateAsync(cancellation);
        }

        private async ValueTask ReconnectAsync(bool isInitialConnection, CancellationToken cancellation)
        {
            await _hubConnection.StopAsync(cancellation);
            await _hubConnection.StartAsync(cancellation);

            // _timeout is not synchronized.
            // It is not necessary.
            // The only ones that access this is we (here) and the keep-alive process. The keep-alive process is ensured to NOT run, when we access.
            if (isInitialConnection)
            {
                (_address, _endPoint, _securityToken, _timeout) = await _hubConnection.InvokeAsync<IServerCallStub, (string address, string endPoint, string securityToken, TimeSpan timeout)>(
                    p => p.ConnectAsync(), cancellation);

                _localEndPointTaskSource.SetResult(new EndPointAddress(_endPoint));
                isInitialConnection = false;
            }
            else
            {
                (_address, _timeout) = await _hubConnection.InvokeAsync<IServerCallStub, (string address, TimeSpan timeout)>(
                    p => p.ReconnectAsync(_endPoint, _securityToken, _address));
            }

            SetLastSendOperation();
        }

        #endregion

        #region Send

        private Task PushToServerAsync(int seqNum, ReadOnlyMemory<byte> payload, CancellationToken cancellation)
        {
            var base64 = Base64Coder.ToBase64String(payload.Span);
            return _hubConnection.InvokeAsync<IServerCallStub>(p => p.PushAsync(seqNum, _endPoint, _securityToken, base64), cancellation);
        }

        private Task SendAckAsync(int seqNum, CancellationToken cancellation)
        {
            return _hubConnection.InvokeAsync<IServerCallStub>(p => p.AckAsync(seqNum), cancellation);
        }

        private Task SendBadMessageAsync(int seqNum, CancellationToken cancellation)
        {
            return _hubConnection.InvokeAsync<IServerCallStub>(p => p.BadMessageAsync(seqNum), cancellation);
        }

        #endregion

        #region Receive

        private async Task ReceiveAsync(int seqNum, ReadOnlyMemory<byte> payload)
        {
            _logger?.LogDebug($"Received message ({payload.Length} total bytes) with seq-num {seqNum}.");

            var message = new Message();
            message.Read(payload.Span);

            await _rxQueue.EnqueueAsync(message);
            await SendAckAsync(seqNum, cancellation: default);
        }

        private void ReceiveAckAsync(int seqNum)
        {
            _logger?.LogDebug($"Received acknowledgment for seq-num {seqNum}.");

            var success = _txQueue.TryRemove(seqNum, out var entry) &&
                          entry.ackSource.TrySetResult(null);
            Debug.Assert(success);
        }

        #endregion

        #region KeepAliveProcess

        // Ack and BadMessage MUST not call this, as these do not validate the client session on the server.
        private void SetLastSendOperation()
        {
            var now = DateTime.UtcNow; // TODO: Use IDateTimeProvider

            lock (_lastSendOperationLock)
            {
                if (now > _lastSendOperation)
                    _lastSendOperation = now;
            }
        }

        private async Task KeepAliveProcess(CancellationToken cancellation)
        {
            var timeout = _timeout;
            var timoutHalf = new TimeSpan(timeout.Ticks / 2);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var now = DateTime.UtcNow; // TODO: Use IDateTimeProvider
                    DateTime lastSendOperation;

                    lock (_lastSendOperationLock)
                    {
                        lastSendOperation = _lastSendOperation;
                    }

                    var delay = timoutHalf - (now - lastSendOperation);

                    if (delay <= TimeSpan.Zero)
                    {
                        await PushToServerAsync(GetNextSeqNum(), ReadOnlyMemory<byte>.Empty, cancellation);
                        SetLastSendOperation();
                    }
                    else
                    {
                        await Task.Delay(delay, cancellation);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    Console.WriteLine("Error in kap: " + exc.ToString());
                    // TODO: Log
                }
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource != null)
            {
                using (disposalSource)
                {
                    // TODO: Log
                    disposalSource.Cancel();

                    _reconnectionManager.Dispose();
                    _hubConnection.Closed -= UnderlyingConnectionLostAsync;
                    _hubConnection.StopAsync().HandleExceptions(); // TODO
                    _stubRegistration.Dispose();
                    _keepAliveProcess.Terminate();
                }
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

        private sealed class ClientCallStub : IClientCallStub
        {
            private readonly ClientEndPoint _endPoint;

            public ClientCallStub(ClientEndPoint endPoint)
            {
                Debug.Assert(endPoint != null);
                _endPoint = endPoint;
            }

            public async Task PushAsync(int seqNum, string payload)
            {
                using (payload.Base64Decode(out var bytes))
                {
                    await _endPoint.ReceiveAsync(seqNum, bytes);
                }
            }

            public Task AckAsync(int seqNum)
            {
                _endPoint.ReceiveAckAsync(seqNum);
                return Task.CompletedTask;
            }

            public Task BadMessageAsync(int seqNum)
            {
                return Task.CompletedTask; // TODO
            }

            public Task BadClientAsync()
            {
                return Task.CompletedTask; // TODO
            }
        }

        private sealed class ReconnectionManager : ReconnectionManagerBase
        {
            private readonly ClientEndPoint _clientEndPoint;

            public ReconnectionManager(ClientEndPoint clientEndPoint, ILogger logger = null) : base(logger)
            {
                Debug.Assert(clientEndPoint != null);
                _clientEndPoint = clientEndPoint;
            }

            protected override ValueTask OnConnectionEstablished(CancellationToken cancellation)
            {
                return _clientEndPoint.OnConnectionEstablished(cancellation);
            }

            protected override ValueTask OnConnectionEstablishing(CancellationToken cancellation)
            {
                return _clientEndPoint.OnConnectionEstablishing(cancellation);
            }

            protected override ValueTask EstablishConnectionAsync(bool isInitialConnection, CancellationToken cancellation)
            {
                return _clientEndPoint.ReconnectAsync(isInitialConnection, cancellation);
            }
        }
    }
}
