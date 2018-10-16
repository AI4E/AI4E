using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Routing.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    /*internal*/
    public sealed class ServerCallStub : Hub<ICallStub>, IServerCallStub
    {
        private readonly ServerEndPoint _endPoint;

        public ServerCallStub(ServerEndPoint endPoint)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            _endPoint = endPoint;
        }

        public Task DeliverAsync(int seqNum, string base64) // byte[] bytes)
        {
            return _endPoint.ReceiveAsync(seqNum, Convert.FromBase64String(base64), Context.ConnectionId);
        }

        public Task AckAsync(int seqNum)
        {
            _endPoint.Ack(seqNum, Context.ConnectionId);

            return Task.CompletedTask;
        }

        public async Task<string> InitAsync(string previousAddress)
        {
            await _endPoint.InitAsync(Context.ConnectionId, previousAddress);
            return Context.ConnectionId;
        }
    }

    // TODO: Are HubContext, IHubClients, etc. thread-safe? (See InitAsync)
    public sealed class ServerEndPoint : IServerEndPoint
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServerEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<(IMessage message, string address)> _inboundMessages;
        private readonly OutboundMessageLookup _outboundMessages = new OutboundMessageLookup();

        private int _nextSeqNum = 1;

        public ServerEndPoint(IServiceProvider serviceProvider, ILogger<ServerEndPoint> logger)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
            _logger = logger;

            _inboundMessages = new AsyncProducerConsumerQueue<(IMessage message, string address)>();
        }

        public Task<(IMessage message, string address)> ReceiveAsync(CancellationToken cancellation = default)
        {
            return _inboundMessages.DequeueAsync(cancellation);
        }

        public async Task SendAsync(IMessage message, string address, CancellationToken cancellation = default)
        {
            var bytes = new byte[message.Length];

            using (var stream = new MemoryStream(bytes, writable: true))
            {
                await message.WriteAsync(stream, cancellation);
            }

            await SendAsync(bytes, address, cancellation);
        }

        internal async Task ReceiveAsync(int seqNum, byte[] bytes, string address)
        {
            var message = new Message();

            using (var stream = new MemoryStream(bytes))
            {
                await message.ReadAsync(stream, cancellation: default);
            }

            await _inboundMessages.EnqueueAsync((message, address));

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();

            await hubContext.Clients.Client(address).AckAsync(seqNum);
        }



        internal void Ack(int seqNum, string address)
        {
            var success = _outboundMessages.TryRemove(seqNum, out var compareAddress, out _, out var ackSource) &&
                          ackSource.TrySetResult(null);
            Assert(success);
            Assert(compareAddress == address);
        }

        internal async Task InitAsync(string address, string previousAddress)
        {
            if (previousAddress == null)
                return;

            var messages = _outboundMessages.Update(previousAddress, address);

            // TODO: Are HubContext, IHubClients, etc. thread-safe?
            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();
            await Task.WhenAll(messages.Select(p => hubContext.Clients.Client(address).DeliverAsync(p.seqNum, Convert.ToBase64String(p.bytes))));
        }

        private async Task SendAsync(byte[] bytes, string address, CancellationToken cancellation)
        {
            var ackSource = new TaskCompletionSource<object>();
            var seqNum = GetNextSeqNum();

            while (!_outboundMessages.TryAdd(seqNum, address, bytes, ackSource))
            {
                seqNum = GetNextSeqNum();
            }

            bool TryCancelSend()
            {
                return _outboundMessages.TryRemove(seqNum, out _, out _, out _) && ackSource.TrySetCanceled();
            }

            void CancelSend()
            {
                var result = TryCancelSend();

                Assert(result);
            }

            try
            {
                using (cancellation.Register(CancelSend))
                {
                    var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();
                    await Task.WhenAll(hubContext.Clients.Client(address).DeliverAsync(seqNum, Convert.ToBase64String(bytes)), ackSource.Task);
                }
            }
            catch
            {
                TryCancelSend();
                throw;
            }
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        public void Dispose() { }
    }
}
