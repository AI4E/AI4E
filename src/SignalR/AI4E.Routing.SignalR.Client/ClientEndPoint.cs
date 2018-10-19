using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

#if BLAZOR
using Blazor.Extensions;
using AI4E.Routing.SignalR.Client;
#else
using Microsoft.AspNetCore.SignalR.Client;
#endif

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    // TODO: Logging
    // TODO: Does the disposal have to be thread-safe? Use AsyncDisposeHelper?
    public sealed class ClientEndPoint : IClientEndPoint, IDisposable
    {
        #region Fields

        private readonly HubConnection _hubConnection;
        private readonly ILogger<ClientEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<IMessage> _inboundMessages;
        private readonly ConcurrentDictionary<int, (byte[] bytes, TaskCompletionSource<object> ackSource)> _outboundMessages;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly ClientCallStub _client;
        private readonly IDisposable _stubRegistration;
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly CancellationTokenSource _disposalSource = new CancellationTokenSource();
        private readonly AsyncReaderWriterLock _disposalLock = new AsyncReaderWriterLock();
        private bool _isDisposed;
        private int _nextSeqNum;
        private string _id = string.Empty;

        #endregion

        #region C'tor

        public ClientEndPoint(HubConnection hubConnection, ILogger<ClientEndPoint> logger = null)
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            _hubConnection = hubConnection;
            _logger = logger;
            _inboundMessages = new AsyncProducerConsumerQueue<IMessage>();
            _outboundMessages = new ConcurrentDictionary<int, (byte[] bytes, TaskCompletionSource<object> ackSource)>();

#if BLAZOR
            _hubConnection.OnClose(UnderlyingConnectionClosedAsync);
#else
            _hubConnection.Closed += UnderlyingConnectionClosedAsync;
#endif

            _client = new ClientCallStub(this);
            _stubRegistration = _hubConnection.Register(_client);
            _initializationHelper = new AsyncInitializationHelper(ConnectAsync);
        }

        #endregion

        #region IClientEndPoint

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            using (await _disposalLock.ReaderLockAsync(cancellation))
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposalSource.Token);
                var combinedCancellation = combinedCancellationSource.Token;

                try
                {
                    await _initializationHelper.Initialization.WithCancellation(combinedCancellation);

                    return await _inboundMessages.DequeueAsync(combinedCancellation);
                }
                catch (OperationCanceledException) when (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task SendAsync(IMessage message, CancellationToken cancellation)
        {
            using (await _disposalLock.ReaderLockAsync(cancellation))
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposalSource.Token);
                var combinedCancellation = combinedCancellationSource.Token;

                try
                {
                    await _initializationHelper.Initialization.WithCancellation(combinedCancellation);

                    var bytes = new byte[message.Length];

                    using (var stream = new MemoryStream(bytes, writable: true))
                    {
                        await message.WriteAsync(stream, combinedCancellation);
                    }

                    await SendAsync(bytes, combinedCancellation);
                }
                catch (OperationCanceledException) when (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        #region Protocol implementation

        private async Task ReceiveAsync(int seqNum, byte[] bytes)
        {
            _logger?.LogDebug($"Received message ({bytes.Length} total bytes) with seq-num {seqNum}.");

            var message = new Message();

            using (var stream = new MemoryStream(bytes))
            {
                await message.ReadAsync(stream, cancellation: default);
            }

            await _inboundMessages.EnqueueAsync(message);
            await _hubConnection.InvokeAsync<ICallStub>(p => p.AckAsync(seqNum));
        }

        private async Task SendAsync(byte[] bytes, CancellationToken cancellation)
        {
            var ackSource = new TaskCompletionSource<object>();
            var seqNum = GetNextSeqNum();

            while (!_outboundMessages.TryAdd(seqNum, (bytes, ackSource)))
            {
                seqNum = GetNextSeqNum();
            }

            bool TryCancelSend()
            {
                return _outboundMessages.TryRemove(seqNum, out _) && ackSource.TrySetCanceled();
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
                    _logger?.LogDebug($"Sending message ({bytes.Length} total bytes) with seq-num {seqNum}.");

                    await Task.WhenAll(_hubConnection.InvokeAsync<ICallStub>(p => p.DeliverAsync(seqNum, Convert.ToBase64String(bytes)), cancellation), ackSource.Task);
                }
            }
            catch
            {
                TryCancelSend();
                throw;
            }
        }

        private void Ack(int seqNum)
        {
            _logger?.LogDebug($"Received acknowledgment for seq-num {seqNum}.");

            var success = _outboundMessages.TryRemove(seqNum, out var entry) &&
                          entry.ackSource.TrySetResult(null);
            Assert(success);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        private async Task UnderlyingConnectionClosedAsync(Exception exception)
        {
            await ConnectAsync(cancellation: _disposalSource.Token);
            await Task.WhenAll(_outboundMessages.ToList().Select(p => _hubConnection.InvokeAsync<ICallStub>(q => q.DeliverAsync(p.Key, Convert.ToBase64String(p.Value.bytes)), cancellation: default)));
        }

        private async Task ConnectAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug("Trying to reconnect to server.");

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
#if BLAZOR
                        await _hubConnection.StartAsync();
#else
                        await _hubConnection.StartAsync(cancellation);
#endif
                        _id = await _hubConnection.InvokeAsync<IServerCallStub, string>(p => p.InitAsync(_id), cancellation);
                        break;
                    }
                    catch (ObjectDisposedException) { throw; }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        _logger?.LogWarning($"Reconnection failed. Trying again in {timeToWait.TotalSeconds} sec.");

                        await Task.Delay(timeToWait, cancellation);

                        if (timeToWait < timeToWaitMax)
                            timeToWait = new TimeSpan(timeToWait.Ticks * 2);
                    }
                }
            }
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            _disposalSource.Cancel();

            using (_disposalLock.WriterLock())
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;

#if !BLAZOR
                _hubConnection.Closed -= UnderlyingConnectionClosedAsync;
#endif
                _stubRegistration.Dispose();
                _initializationHelper.Cancel();
            }
        }

        #endregion

        private sealed class ClientCallStub : ICallStub
        {
            private readonly ClientEndPoint _endPoint;

            public ClientCallStub(ClientEndPoint endPoint)
            {
                Assert(endPoint != null);
                _endPoint = endPoint;
            }

            public Task DeliverAsync(int seqNum, string base64) // byte[] bytes)
            {
                return _endPoint.ReceiveAsync(seqNum, Convert.FromBase64String(base64));
            }

            public Task AckAsync(int seqNum)
            {
                _endPoint.Ack(seqNum);
                return Task.CompletedTask;
            }
        }
    }
}
