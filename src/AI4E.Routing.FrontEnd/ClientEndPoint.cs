using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.FrontEnd
{
    // TODO: Logging
    public sealed class ClientEndPoint : IClientEndPoint, IDisposable
    {
        private readonly HubConnection _hubConnection;
        private readonly ILogger<ClientEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<IMessage> _inbox;
        private readonly ConcurrentDictionary<int, byte[]> _outbox;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly ClientCallStub _client;
        private readonly IDisposable _stubRegistration;
        private int _nextSeqNum = 1;

        public ClientEndPoint(HubConnection hubConnection, ILogger<ClientEndPoint> logger = null)
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            _hubConnection = hubConnection;
            _logger = logger;
            _inbox = new AsyncProducerConsumerQueue<IMessage>();
            _outbox = new ConcurrentDictionary<int, byte[]>();
            _hubConnection.Closed += UnderlyingConnectionClosedAsync;
            _client = new ClientCallStub(this);
            _stubRegistration = _hubConnection.Register(_client);
            _initializationHelper = new AsyncInitializationHelper(ConnectAsync);
        }

        private sealed class ClientCallStub : ICallStub
        {
            private readonly ClientEndPoint _endPoint;

            public ClientCallStub(ClientEndPoint endPoint)
            {
                Assert(endPoint != null);
                _endPoint = endPoint;
            }

            public Task DeliverAsync(int seqNum, byte[] bytes)
            {
                return _endPoint.ReceiveAsync(seqNum, bytes);
            }

            public Task AckAsync(int seqNum)
            {
                _endPoint.Ack(seqNum);
                return Task.CompletedTask;
            }
        }

        #region Public interface

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);

            return await _inbox.DequeueAsync(cancellation);
        }

        public async Task SendAsync(IMessage message, CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);

            var bytes = new byte[message.Length];

            using (var stream = new MemoryStream(bytes, writable: true))
            {
                await message.WriteAsync(stream, cancellation);
            }

            await SendAsync(bytes, cancellation);
        }

        #endregion

        private async Task ReceiveAsync(int seqNum, byte[] bytes)
        {
            var message = new Message();

            using (var stream = new MemoryStream(bytes))
            {
                await message.ReadAsync(stream, cancellation: default);
            }

            await _inbox.EnqueueAsync(message);
            await _hubConnection.InvokeAsync<ICallStub>(p => p.AckAsync(seqNum));
        }

        private Task SendAsync(byte[] bytes, CancellationToken cancellation)
        {
            var seqNum = GetNextSeqNum();

            while (!_outbox.TryAdd(seqNum, bytes))
            {
                seqNum = GetNextSeqNum();
            }

            return _hubConnection.InvokeAsync<ICallStub>(p => p.DeliverAsync(seqNum, bytes), cancellation);
        }

        private void Ack(int seqNum)
        {
            _outbox.TryRemove(seqNum, out _);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        private async Task UnderlyingConnectionClosedAsync(Exception exception)
        {
            await ConnectAsync(cancellation: default);
            await Task.WhenAll(_outbox.ToList().Select(p => _hubConnection.InvokeAsync<ICallStub>(q => q.DeliverAsync(p.Key, p.Value), cancellation: default)));
        }

        private string _id = null;

        private readonly AsyncLock _lock = new AsyncLock();

        private async Task ConnectAsync(CancellationToken cancellation)
        {
            using (await _lock.LockAsync())
            {
                // We are waiting one second after the first failed attempt to connect.
                // For each failed attempt, we increase the waited time to the next connection attempt,
                // until we reach an upper limit of 12 seconds.
                var timeToWait = new TimeSpan(1000 * TimeSpan.TicksPerMillisecond);
                var timeToWaitMax = new TimeSpan(12000 * TimeSpan.TicksPerMillisecond);

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        await _hubConnection.StartAsync(cancellation);
                        _id = await _hubConnection.InvokeAsync<IServerCallStub, string>(p => p.InitAsync(_id), cancellation);
                        break;
                    }
                    catch (ObjectDisposedException) { throw; }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        await Task.Delay(timeToWait, cancellation);

                        if (timeToWait < timeToWaitMax)
                            timeToWait = new TimeSpan(timeToWait.Ticks * 2);
                    }
                }
            }
        }

        public void Dispose()
        {
            _hubConnection.Closed -= UnderlyingConnectionClosedAsync;
            _stubRegistration.Dispose();
            _initializationHelper.Cancel();
        }
    }
}
