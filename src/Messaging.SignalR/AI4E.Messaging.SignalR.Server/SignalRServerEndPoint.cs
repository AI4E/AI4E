using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Messaging.SignalR.Client;
using AI4E.Remoting;
using AI4E.Utils;
using AI4E.Utils.Memory;
using AI4E.Utils.Messaging.Primitives;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Messaging.SignalR.Server
{
    public sealed class SignalRServerEndPoint : IDisposable, ISignalRServerEndPoint
    {
        #region Fields

        private readonly IClientConnectionManager _connectionManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SignalRServerEndPoint> _logger;

        private readonly AsyncProducerConsumerQueue<SignalRServerPacket> _rxQueue;
        private readonly ConcurrentDictionary<int, AckLookupEntry> _ackLookup;
        private readonly ConcurrentDictionary<RouteEndPointAddress, ClientConnection> _clients;
        private readonly RequestReplyEndPoint<SignalRServerPacket> _reqRplyEndPoint;
        private int _nextSeqNum = 1;

        #endregion

        #region C'tor

        public SignalRServerEndPoint(
            IClientConnectionManager connectionManager,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory = null)
        {
            if (connectionManager == null)
                throw new ArgumentNullException(nameof(connectionManager));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _connectionManager = connectionManager;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory?.CreateLogger<SignalRServerEndPoint>();

            _rxQueue = new AsyncProducerConsumerQueue<SignalRServerPacket>();
            _ackLookup = new ConcurrentDictionary<int, AckLookupEntry>();
            _clients = new ConcurrentDictionary<RouteEndPointAddress, ClientConnection>();

            var wrapper = new ServerEndPoint(this);
            var requestReplyEndPointLogger = loggerFactory?.CreateLogger<RequestReplyEndPoint<SignalRServerPacket>>();
            _reqRplyEndPoint = new RequestReplyEndPoint<SignalRServerPacket>(wrapper, requestReplyEndPointLogger);
        }

        #endregion

        #region ISignalRServerEndPoint 

        public ValueTask<MessageReceiveResult<SignalRServerPacket>> ReceiveAsync(CancellationToken cancellation)
        {
            return _reqRplyEndPoint.ReceiveAsync(cancellation);
        }

        public ValueTask<MessageSendResult> SendAsync(SignalRServerPacket packet, CancellationToken cancellation)
        {
            return _reqRplyEndPoint.SendAsync(packet, cancellation);
        }

        private ValueTask SendInternalAsync(SignalRServerPacket packet, CancellationToken cancellation)
        {
            if (!_clients.TryGetValue(packet.RemoteEndPoint, out var client))
            {
                _logger?.LogError($"Unable to send message to client {packet.RemoteEndPoint}: No such client.");

                return default; // TODO: Throw an exception?
            }

            return client.SendAsync(packet.Message, cancellation);
        }

        private ValueTask<SignalRServerPacket> ReceiveInternalAsync(CancellationToken cancellation)
        {
            return _rxQueue.DequeueAsync(cancellation).AsValueTask();
        }

        #endregion

        #region Send

        private async Task PushToClientAsync(string address, int seqNum, Message message)
        {
            var client = GetClientCallStub(address);

            using var memoryOwner = MemoryPool<byte>.Shared.RentExact(message.Length);
            var memory = memoryOwner.Memory;
            var base64 = Base64Coder.ToBase64String(memory.Span);
            await client.PushAsync(seqNum, base64);
        }

        private Task SendAckAsync(string address, int seqNum)
        {
            var client = GetClientCallStub(address);

            return client.AckAsync(seqNum);
        }

        private Task SendBadMessageAsync(string address, int seqNum)
        {
            var client = GetClientCallStub(address);

            return client.BadMessageAsync(seqNum);
        }

        private Task SendBadClientResponseAsync(string address)
        {
            var client = GetClientCallStub(address);

            return client.BadClientAsync();
        }

        #endregion

        #region Receive

        private async Task ReceiveAsync(int seqNum,
                                        string address,
                                        RouteEndPointAddress endPoint,
                                        string securityToken,
                                        ReadOnlyMemory<byte> payload)
        {
            if (!await _connectionManager.ValidateClientAsync(new ClientCredentials(endPoint, securityToken), cancellation: default))
            {
                await SendBadClientResponseAsync(address);
                return;
            }

            if (!payload.IsEmpty)
            {
                var message = Message.ReadFromMemory(payload.Span);
                await _rxQueue.EnqueueAsync(new SignalRServerPacket(message, endPoint));
                await SendAckAsync(address, seqNum);
            }
        }

        private void ReceiveAckAsync(int seqNum, string address)
        {
            _logger?.LogDebug($"Received acknowledgment for seq-num {seqNum} from client {address}.");

            // We cannot assume that this is successful,
            // as the client may already have received a message,
            // when we abort the transmission and therefore sends an ack,
            // altough we already removed the entry from the ack-lookup.
            if (_ackLookup.TryRemove(seqNum, out var entry))
            {
                entry.AckSource.TrySetResult(null);
            }

            // We cannot assume that the ack of a message is received from the client, that we are currently connected with.
            //Assert(LookupAddress(endPoint) == address);
        }

        private void ReceiveBadMessageAsync(int seqNum, string address)
        {
            // TODO: Log
            // TODO: Shutdown connection, etc.
        }

        // The client connects to the cluster for the first time.
        private async Task<(string endPoint, string securityToken, TimeSpan timeout)> ConnectAsync(string address)
        {
            var credentials = await _connectionManager.AddClientAsync(cancellation: default);
            _clients[credentials.EndPoint] = new ClientConnection(this, credentials.EndPoint, address);
            return (credentials.EndPoint.ToString(), credentials.SecurityToken, _connectionManager.Timeout);
        }

        // The client does reconnect to the cluster, but may connect to this cluster node the first time (previousAddress is null or belongs to other cluster node).
        private async Task<TimeSpan> ReconnectAsync(string address, RouteEndPointAddress endPoint, string securityToken, string previousAddress)
        {
            if (!await _connectionManager.ValidateClientAsync(new ClientCredentials(endPoint, securityToken), cancellation: default))
            {
                await SendBadClientResponseAsync(address);
                return default;
            }

            var client = _clients.GetOrAdd(endPoint, _ => new ClientConnection(this, endPoint, address));

            _logger?.LogDebug($"Client {address} (re-)initiated connection.");
            await client.ReconnectAsync(address);
            return _connectionManager.Timeout;
        }

        #endregion

        private IClientCallStub GetClientCallStub(string address)
        {
            // TODO: Can this be cached? Does this provide as any perf benefits?
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, IClientCallStub>>();

            return hubContext.Clients.Client(address);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        private sealed class ServerEndPoint : IEndPoint<SignalRServerPacket>
        {
            private readonly SignalRServerEndPoint _serverEndPoint;

            public ServerEndPoint(SignalRServerEndPoint serverEndPoint)
            {
                _serverEndPoint = serverEndPoint;
            }

            public ValueTask<SignalRServerPacket> ReceiveAsync(CancellationToken cancellation)
            {
                return _serverEndPoint.ReceiveInternalAsync(cancellation);
            }

            public ValueTask SendAsync(SignalRServerPacket packet, CancellationToken cancellation)
            {
                return _serverEndPoint.SendInternalAsync(packet, cancellation);
            }

            public void Dispose()
            {
                _serverEndPoint.Dispose();
            }
        }

        internal sealed class ServerCallStub : Hub<IClientCallStub>, IServerCallStub
        {
            private readonly SignalRServerEndPoint _endPoint;

            public ServerCallStub(SignalRServerEndPoint endPoint)
            {
                Assert(endPoint != null);

                _endPoint = endPoint;
            }

            public async Task PushAsync(int seqNum, string endPoint, string securityToken, string payload)
            {
                if (payload == string.Empty)
                {
                    await _endPoint.ReceiveAsync(seqNum, Context.ConnectionId, new RouteEndPointAddress(endPoint), securityToken, ReadOnlyMemory<byte>.Empty);
                    return;
                }

                using var bytesOwner = payload.Base64Decode(MemoryPool<byte>.Shared);
                var bytes = bytesOwner.Memory;
                await _endPoint.ReceiveAsync(seqNum, Context.ConnectionId, new RouteEndPointAddress(endPoint), securityToken, bytes);
            }

            public Task AckAsync(int seqNum)
            {
                _endPoint.ReceiveAckAsync(seqNum, Context.ConnectionId);
                return Task.CompletedTask;
            }

            public Task BadMessageAsync(int seqNum)
            {
                _endPoint.ReceiveBadMessageAsync(seqNum, Context.ConnectionId);
                return Task.CompletedTask;
            }

            public async Task<(string address, string endPoint, string securityToken, TimeSpan timeout)> ConnectAsync()
            {
                var (endPoint, securityToken, timeout) = await _endPoint.ConnectAsync(Context.ConnectionId);
                return (Context.ConnectionId, endPoint, securityToken, timeout);
            }

            public async Task<(string address, TimeSpan timeout)> ReconnectAsync(string endPoint, string securityToken, string previousAddress)
            {
                var timeout = await _endPoint.ReconnectAsync(Context.ConnectionId, new RouteEndPointAddress(endPoint), securityToken, previousAddress);
                return (Context.ConnectionId, timeout);
            }
        }

        private sealed class ClientConnection
        {
            private readonly SignalRServerEndPoint _serverEndPoint;
            private readonly RouteEndPointAddress _endPoint;
            private string _address;
            private readonly object _addressLock = new object();
            private readonly ConcurrentDictionary<int, (Message message, TaskCompletionSource<object> ackSource)> _txLookup;

            private readonly Task _completion;
            private volatile CancellationTokenSource _completionSource = new CancellationTokenSource();

            public ClientConnection(SignalRServerEndPoint serverEndPoint, RouteEndPointAddress endPoint, string address)
            {
                Assert(serverEndPoint != null);

                _serverEndPoint = serverEndPoint;
                _endPoint = endPoint;
                _address = address;

                _txLookup = new ConcurrentDictionary<int, (Message message, TaskCompletionSource<object> ackSource)>();
                _completion = ClientDisconnectionWithEntryRemoval();
            }

            #region Disconnection

            private async Task ClientDisconnectionWithEntryRemoval()
            {
                try
                {
                    await _serverEndPoint._connectionManager.WaitForDisconnectAsync(_endPoint, cancellation: default);
                }
                finally
                {
                    OnDisconnected();
                }
            }

            private void OnDisconnected()
            {
                var completionSource = Interlocked.Exchange(ref _completionSource, null);

                if (completionSource != null)
                {
                    completionSource.Cancel();
                    CancelTxMessageAckWaiting();
                    completionSource.Dispose();
                }
            }

            private void CancelTxMessageAckWaiting()
            {
                if (_serverEndPoint._clients.Remove(_endPoint, this))
                {
                    foreach (var (seqNum, ackSource) in _txLookup.Select(p => (seqNum: p.Key, p.Value.ackSource)))
                    {
                        ackSource.SetCanceled();
                        _serverEndPoint._ackLookup.TryRemove(seqNum, out _);
                    }
                }
            }

            private IDisposable CheckDisposal(ref CancellationToken cancellation,
                                              out CancellationToken externalCancellation,
                                              out CancellationToken disposal)
            {
                var disposalSource = _completionSource; // Volatile read op

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

            public async Task ReconnectAsync(string address)
            {
                var cancellation = default(CancellationToken);
                using (CheckDisposal(ref cancellation, out _, out var disposal))
                {
                    lock (_addressLock)
                    {
                        _address = address;
                    }

                    var messages = _txLookup.Select(p => (seqNum: p.Key, p.Value.message));

                    var tasks = new List<Task>();

                    foreach (var (seqNum, message) in messages)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        tasks.Add(_serverEndPoint.PushToClientAsync(address, seqNum, message));
                    }

                    await Task.WhenAll(tasks).WithCancellation(cancellation);
                }
            }

            public async ValueTask SendAsync(Message message, CancellationToken cancellation)
            {
                using (CheckDisposal(ref cancellation, out _, out var disposal))
                {
                    var ackSource = new TaskCompletionSource<object>();
                    var seqNum = _serverEndPoint.GetNextSeqNum();

                    while (!_serverEndPoint._ackLookup.TryAdd(seqNum, new AckLookupEntry(seqNum, _endPoint, message, ackSource)))
                    {
                        seqNum = _serverEndPoint.GetNextSeqNum();
                    }

                    _txLookup[seqNum] = (message, ackSource);
                    try
                    {
                        try
                        {
                            // We cannot assume that the operation is truly cancelled. 
                            // It is possible that the cancellation is invoked, when the message is just acked,
                            // but before the delegate is unregistered from the cancellation token.

                            _serverEndPoint._logger?.LogDebug($"Sending message ({message.Length} total bytes) with seq-num {seqNum} to client {_endPoint}.");

                            string address;

                            lock (_addressLock)
                            {
                                address = _address;
                            }

                            await _serverEndPoint.PushToClientAsync(address, seqNum, message).WithCancellation(cancellation);
                            await ackSource.Task.WithCancellation(cancellation);
                        }
                        catch (Exception exc)
                        {
                            if (_serverEndPoint._ackLookup.TryRemove(seqNum, out _))
                            {
                                ackSource.TrySetExceptionOrCanceled(exc);
                            }

                            throw;
                        }

                    }
                    finally
                    {
                        _txLookup.TryRemove(seqNum, out _);
                    }
                }
            }
        }

        private readonly struct AckLookupEntry
        {
            public AckLookupEntry(int seqNum, RouteEndPointAddress endPoint, Message message, TaskCompletionSource<object> ackSource)
            {
                SeqNum = seqNum;
                EndPoint = endPoint;
                Message = message;
                AckSource = ackSource;
            }

            public int SeqNum { get; }
            public RouteEndPointAddress EndPoint { get; }
            public Message Message { get; }
            public TaskCompletionSource<object> AckSource { get; }
        }

        public void Dispose()
        {
            _reqRplyEndPoint.Dispose();
        }
    }
}
