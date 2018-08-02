using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Routing.FrontEnd
{
    internal sealed class ServerCallStub : Hub<ICallStub>, IServerCallStub
    {
        private readonly ServerEndPoint _endPoint;

        public ServerCallStub(ServerEndPoint endPoint)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            _endPoint = endPoint;
        }

        public Task DeliverAsync(int seqNum, byte[] bytes)
        {
            return _endPoint.ReceiveAsync(seqNum, bytes, Context.ConnectionId);
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

    public sealed class ServerEndPoint : IServerEndPoint
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServerEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<(IMessage message, string address)> _inbox;
        private readonly ConcurrentDictionary<int, (byte[] message, string address)> _outbox;

        private int _nextSeqNum = 1;

        public ServerEndPoint(IServiceProvider serviceProvider, ILogger<ServerEndPoint> logger)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
            _logger = logger;

            _inbox = new AsyncProducerConsumerQueue<(IMessage message, string address)>();
            _outbox = new ConcurrentDictionary<int, (byte[] message, string address)>();
        }

        public Task<(IMessage message, string address)> ReceiveAsync(CancellationToken cancellation = default)
        {
            return _inbox.DequeueAsync(cancellation);
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

            await _inbox.EnqueueAsync((message, address));

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();

            await hubContext.Clients.Client(address).AckAsync(seqNum);
        }

        internal void Ack(int seqNum, string address)
        {
            _outbox.TryRemove(seqNum, out _);
        }

        internal async Task InitAsync(string address, string previousAddress)
        {
            if (previousAddress == null)
                return;

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();

            // TODO: Are HubContext, IHubClients, etc. thread-safe?
            var entries = _outbox.ToList().Where(p => p.Value.address == previousAddress);
            var tasks = new List<Task>();

            foreach (var entry in entries)
            {
                _outbox.TryUpdate(entry.Key, (entry.Value.message, address), entry.Value);
                tasks.Add(hubContext.Clients.Client(address).DeliverAsync(entry.Key, entry.Value.message));
            }

            await Task.WhenAll(tasks);
        }

        private Task SendAsync(byte[] bytes, string address, CancellationToken cancellation)
        {
            var seqNum = GetNextSeqNum();

            while (!_outbox.TryAdd(seqNum, (bytes, address)))
            {
                seqNum = GetNextSeqNum();
            }

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();

            return hubContext.Clients.Client(address).DeliverAsync(seqNum, bytes);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        public void Dispose() { }
    }
}
