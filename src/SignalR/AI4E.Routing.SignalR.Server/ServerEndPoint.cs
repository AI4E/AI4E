using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Remoting;
using AI4E.Routing.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    // TODO: When a client disconnects, we have to remove its messages from the tx-queue and also its address from the lookup.
    //       This can be achieved by handling the "ClientsDisconnected" event of the ConnectedClientLookup.
    //       We have to watch out for the case that a different client registers with exactly the same address, we may lazy remove.
    //       It is theoretically possible to remove the new clients messages.
    public sealed class ServerEndPoint : IDisposable, IServerEndPoint
    {
        #region Fields

        private readonly AsyncProducerConsumerQueue<(IMessage message, EndPointAddress endPoint)> _rxQueue;
        private readonly OutboundMessageLookup _txQueues = new OutboundMessageLookup();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServerEndPoint> _logger;

        // Cluster-wide store of connected clients and their security tokens.
        private readonly IConnectedClientLookup _connectedClients;

        // Local lookup of a clients current physical address
        private readonly Dictionary<EndPointAddress, (string address, Task disonnectionTask)> _clientLookup;
        private readonly object _clientLookupLock = new object();

        private int _nextSeqNum = 1;

        #endregion

        #region C'tor

        public ServerEndPoint(IConnectedClientLookup connectedClients, IServiceProvider serviceProvider, ILogger<ServerEndPoint> logger = null)
        {
            if (connectedClients == null)
                throw new ArgumentNullException(nameof(connectedClients));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _connectedClients = connectedClients;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _rxQueue = new AsyncProducerConsumerQueue<(IMessage message, EndPointAddress endPoint)>();
            _clientLookup = new Dictionary<EndPointAddress, (string address, Task disonnectionTask)>();
        }

        #endregion

        #region IServerEndPoint 

        public async Task SendAsync(IMessage message, EndPointAddress endPoint, CancellationToken cancellation)
        {
            var ackSource = new TaskCompletionSource<object>();

            // TODO
            var bytes = new byte[message.Length];
            message.Write(bytes);

            var seqNum = GetNextSeqNum();

            while (!_txQueues.TryAdd(seqNum, endPoint, bytes, ackSource))
            {
                seqNum = GetNextSeqNum();
            }

            try
            {
                // We cannot assume that the operation is truly cancelled. 
                // It is possible that the cancellation is invoked, when the message is just acked,
                // but before the delegate is unregistered from the cancellation token.

                _logger?.LogDebug($"Sending message ({bytes.Length} total bytes) with seq-num {seqNum} to client {endPoint}.");

                var address = LookupAddress(endPoint);

                if (address == null)
                {
                    // TODO
                    throw null;
                }

                await PushToClientAsync(address, seqNum, bytes).WithCancellation(cancellation);
                await ackSource.Task.WithCancellation(cancellation);
            }
            catch (Exception exc)
            {
                if (_txQueues.TryRemove(seqNum))
                {
                    ackSource.TrySetExceptionOrCancelled(exc);
                }

                throw;
            }
        }

        public Task<(IMessage message, EndPointAddress endPoint)> ReceiveAsync(CancellationToken cancellation)
        {
            return _rxQueue.DequeueAsync(cancellation);
        }

        #endregion

        #region AddressTranslation

        private string LookupAddress(EndPointAddress endPoint)
        {
            lock (_clientLookupLock)
            {
                return _clientLookup.TryGetValue(endPoint, out var entry) ? entry.address : null;
            }
        }

        private async Task<bool> ValidateAndUpdateTranslationAsync(string address, EndPointAddress endPoint, string securityToken, CancellationToken cancellation)
        {
            var result = await _connectedClients.ValidateClientAsync(endPoint, securityToken, cancellation);

            if (result)
            {
                lock (_clientLookupLock)
                {
                    var entry = _clientLookup[endPoint];

                    if (entry.address != address)
                    {
                        entry.address = address;
                        _clientLookup[endPoint] = entry;
                    }
                }
            }

            return result;
        }

        private async Task<(EndPointAddress endPoint, string securityToken)> AddTranslationAsync(string address, CancellationToken cancellation)
        {
            var (endPoint, securityToken) = await _connectedClients.AddClientAsync(cancellation: default);
            var disonnectionTask = _connectedClients.WaitForDisconnectAsync(endPoint, cancellation: default);

            Assert(!disonnectionTask.IsCompleted);

            lock (_clientLookupLock)
            {
                async Task ClientDisconnectionWithEntryRemoval()
                {
                    try
                    {
                        await disonnectionTask;
                    }
                    finally
                    {
                        lock (_clientLookupLock)
                        {
                            _clientLookup.Remove(endPoint);
                        }
                    }
                }

                _clientLookup.Add(endPoint, (address, ClientDisconnectionWithEntryRemoval()));
            }

            return (endPoint, securityToken);
        }

        #endregion

        #region Send

        private Task PushToClientAsync(string address, int seqNum, ReadOnlyMemory<byte> payload)
        {
            var client = GetClient(address);

            return client.PushAsync(seqNum, Base64Coder.ToBase64String(payload.Span));
        }

        private Task SendAckAsync(string address, int seqNum)
        {
            var client = GetClient(address);

            return client.AckAsync(seqNum);
        }

        private Task SendBadMessageAsync(string address, int seqNum)
        {
            var client = GetClient(address);

            return client.BadMessageAsync(seqNum);
        }

        private Task SendBadClientResponseAsync(string address)
        {
            var client = GetClient(address);

            return client.BadClientAsync();
        }

        #endregion

        #region Receive

        private async Task ReceiveAsync(int seqNum,
                                        string address,
                                        EndPointAddress endPoint,
                                        string securityToken,
                                        ReadOnlyMemory<byte> payload)
        {
            if (!await ValidateAndUpdateTranslationAsync(address, endPoint, securityToken, cancellation: default))
            {
                await SendBadClientResponseAsync(address);
                return;
            }

            var message = new Message();
            message.Read(payload.Span);

            await _rxQueue.EnqueueAsync((message, endPoint));
            await SendAckAsync(address, seqNum);
        }

        private void ReceiveAckAsync(int seqNum, string address)
        {
            _logger?.LogDebug($"Received acknowledgment for seq-num {seqNum} from client {address}.");

            var success = _txQueues.TryRemove(seqNum, out var endPoint, out _, out var ackSource) &&
                          ackSource.TrySetResult(null);
            Assert(success);

            // We cannot assume that the ack of a message is received from the client, that we are currently connected with.
            //Assert(LookupAddress(endPoint) == address);
        }

        private void ReceiveBadMessageAsync(int seqNum, string address)
        {
            // TODO: Log
            // TODO: Shutdown connection, etc.
        }

        // The client connects to the cluster for the first time.
        private async Task<(string endPoint, string securityToken)> ConnectAsync(string address)
        {
            var (endPoint, securityToken) = await AddTranslationAsync(address, cancellation: default);

            return (endPoint.ToString(), securityToken);
        }

        // The client does reconnect to the cluster, but may connect to this cluster node the first time (previousAddress is null or belongs to other cluster node.
        private async Task ReconnectAsync(string address, EndPointAddress endPoint, string securityToken, string previousAddress)
        {
            if (!await ValidateAndUpdateTranslationAsync(address, endPoint, securityToken, cancellation: default))
            {
                await SendBadClientResponseAsync(address);
                return;
            }

            if (previousAddress == null /* || previous address is from different cluster node*/) // TODO: Check whether the address is from our cluster node
            {
                _logger?.LogDebug($"Client {address} initiated connection.");
                return;
            }

            _logger?.LogDebug($"Client {address} with previous address {previousAddress} re-initiated connection.");

            var messages = _txQueues.GetAll(endPoint);

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, IClientCallStub>>();
            var tasks = new List<Task>();

            foreach (var (seqNum, payload) in messages)
            {
                tasks.Add(PushToClientAsync(address, seqNum, payload));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        private IClientCallStub GetClient(string address)
        {
            // TODO: Can this be cached? Does this provide as any perf benefits?
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, IClientCallStub>>();

            return hubContext.Clients.Client(address);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        internal sealed class ServerCallStub : Hub<IClientCallStub>, IServerCallStub
        {
            private readonly ServerEndPoint _endPoint;

            public ServerCallStub(ServerEndPoint endPoint)
            {
                Assert(endPoint != null);

                _endPoint = endPoint;
            }

            public async Task PushAsync(int seqNum, string endPoint, string securityToken, string payload)
            {
                using (payload.Base64Decode(out var bytes))
                {
                    await _endPoint.ReceiveAsync(seqNum, Context.ConnectionId, new EndPointAddress(endPoint), securityToken, bytes);
                }
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

            public async Task<(string address, string endPoint, string securityToken)> ConnectAsync()
            {
                var (endPoint, securityToken) = await _endPoint.ConnectAsync(Context.ConnectionId);
                return (Context.ConnectionId, endPoint, securityToken);
            }

            public async Task<string> ReconnectAsync(string endPoint, string securityToken, string previousAddress)
            {
                await _endPoint.ReconnectAsync(Context.ConnectionId, new EndPointAddress(endPoint), securityToken, previousAddress);
                return Context.ConnectionId;
            }
        }

        public void Dispose() { }
    }
}
