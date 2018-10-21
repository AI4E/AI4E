using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;
using System.Buffers;
using System.Diagnostics;

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
    public sealed partial class ClientEndPoint : IClientEndPoint
    {
        #region Fields

        private readonly HubConnection _hubConnection;
        private readonly ILogger<ClientEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<IMessage> _inboundMessages;
        private readonly ConcurrentDictionary<int, (ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)> _outboundMessages;

        private readonly ClientCallStub _client;
        private readonly IDisposable _stubRegistration;
        private readonly AsyncLock _lock = new AsyncLock();

        private volatile CancellationTokenSource _disposalSource = new CancellationTokenSource();

        private int _nextSeqNum;
        private string _id = string.Empty;

        // Indicates the loose of connection on the layer, the client end-point establishes.
        private readonly AsyncConnectionLostEvent _connectionLostEvent = new AsyncConnectionLostEvent(set: true);

        // Indicates the loose of the underlying connection (the signal-r connection; 1 = true, 0 = false)
        private volatile int _underlyingConnectionLost = 1;

        #endregion

        #region C'tor

        public ClientEndPoint(HubConnection hubConnection, ILogger<ClientEndPoint> logger = null)
        {
            if (hubConnection == null)
                throw new ArgumentNullException(nameof(hubConnection));

            _hubConnection = hubConnection;
            _logger = logger;
            _inboundMessages = new AsyncProducerConsumerQueue<IMessage>();
            _outboundMessages = new ConcurrentDictionary<int, (ReadOnlyMemory<byte> bytes, TaskCompletionSource<object> ackSource)>();
#if BLAZOR
            _hubConnection.OnClose(UnderlyingConnectionLostAsync);
#else
            _hubConnection.Closed += UnderlyingConnectionLostAsync;
#endif

            _client = new ClientCallStub(this);
            _stubRegistration = _hubConnection.Register(_client);

            // Intitially, we are unconnected and have to connect the fist time.
            EstablishConnectionAsync(isInitialConnection: true).HandleExceptions();
        }

        #endregion

        #region IClientEndPoint

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            using (CheckDisposal(ref cancellation, out var externalCancellation, out var disposal))
            {
                try
                {
                    return await _inboundMessages.DequeueAsync(cancellation);
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
                    using (ArrayPool<byte>.Shared.Rent((int)message.Length, out var memory))
                    {
                        message.Write(memory);

                        await SendAsync(memory, cancellation);
                    }
                }
                catch (OperationCanceledException) when (disposal.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        #endregion

        #region Protocol implementation

        private async Task ReceiveAsync(int seqNum, ReadOnlyMemory<byte> bytes)
        {
            _logger?.LogDebug($"Received message ({bytes.Length} total bytes) with seq-num {seqNum}.");

            var message = new Message();
            message.Read(bytes);

            await _inboundMessages.EnqueueAsync(message);
            await _hubConnection.InvokeAsync<ICallStub>(p => p.AckAsync(seqNum));
        }

        private async Task SendAsync(ReadOnlyMemory<byte> memory, CancellationToken cancellation)
        {
            var ackSource = new TaskCompletionSource<object>();
            var seqNum = GetNextSeqNum();

            while (!_outboundMessages.TryAdd(seqNum, (memory, ackSource)))
            {
                seqNum = GetNextSeqNum();
            }

            try
            {
                // We cannot assume that the operation is truly cancelled. 
                // It is possible that the cancellation is invoked, when the message is just acked,
                // but before the delegate is unregistered from the cancellation token.

                _logger?.LogDebug($"Sending message ({memory.Length} total bytes) with seq-num {seqNum}.");

                async Task Send()
                {
                    var base64 = Base64Coder.ToBase64String(memory.Span);

                    await _hubConnection.InvokeAsync<ICallStub>(p => p.DeliverAsync(seqNum, base64), cancellation);
                    await ackSource.Task.WithCancellation(cancellation);
                }

                var sendOperation = Send();
                var connectionLost = _connectionLostEvent.WaitAsync();

                var completed = await Task.WhenAny(sendOperation, connectionLost);

                if (completed == connectionLost)
                {
                    // The connection is broken. The message will be re-sent, when reconnected.
                    await ackSource.Task.WithCancellation(cancellation);
                }
            }
            catch
            {
                // The operation was either cancellation from outside or the object is disposed or something is wrong.
                if (_outboundMessages.TryRemove(seqNum, out _))
                {
                    ackSource.TrySetCanceled();
                }

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

        private Task UnderlyingConnectionLostAsync(Exception exception)
        {
            // TODO: Log exception?
            return EstablishConnectionAsync(isInitialConnection: false);
        }

        private async Task EstablishConnectionAsync(bool isInitialConnection)
        {
            var disposalSource = _disposalSource; // Volatile read op

            if (disposalSource == null)
            {
                // We are disposed.
                return;
            }

            Task SendMessage(int seqNum, ReadOnlyMemory<byte> bytes)
            {
                var base64 = Base64Coder.ToBase64String(bytes.Span);
                return _hubConnection.InvokeAsync<ICallStub>(p => p.DeliverAsync(seqNum, base64), cancellation: disposalSource.Token);
            }

            _underlyingConnectionLost = 1; // Volatile read op.

            do
            {
                // If this is false, we are already performing a reconnection concurrently
                // or this is the initial connection.
                var lostConnection = _connectionLostEvent.Set();

                if (lostConnection || isInitialConnection)
                {
                    isInitialConnection = false;

                    // Reconnect
                    await EstablishConnectionCoreAsync(cancellation: disposalSource.Token); // TODO: This may throw.

                    // Resend all messages
                    await Task.WhenAll(_outboundMessages.ToList().Select(p => SendMessage(seqNum: p.Key, bytes: p.Value.bytes))); // TODO: This may throw.

                    _connectionLostEvent.Reset();
                }
            }
            while (_underlyingConnectionLost == 1); // Volatile read op.
            // We have to perform this in a loop to prevent a race condition that,
            // We set' the _connectionLostEvent, preventing other to concurrenty re-establish the connection and we currently are just before the resetting the event.
            // Concurrently, the operation is performed for another time, as the connection is lost, while we perform out operation.
            // The concurrent operation connection enter the if-condition, as it did not set' the event actually.
            // This would lead to a situation of lost wake-up, that the connection is factual lost, but we do not reconnect.
        }

        private async Task EstablishConnectionCoreAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug("Trying to (re)connect to server.");

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
                        // We will re-establish the underlying connection now. => Reset the connection lost indicator.
                        _underlyingConnectionLost = 0;
#if BLAZOR
                        await _hubConnection.StartAsync();
#else
                        await _hubConnection.StartAsync(cancellation);
#endif
                        _id = await _hubConnection.InvokeAsync<IServerCallStub, string>(p => p.InitAsync(_id), cancellation);

                        // The underlying connection was not lost in the meantime.
                        if (_underlyingConnectionLost == 0)
                        {
                            break;
                        }

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
            var disposalSource = Interlocked.Exchange(ref _disposalSource, null);

            if (disposalSource != null)
            {
                // TODO: Log

#if !BLAZOR
                _hubConnection.Closed -= UnderlyingConnectionLostAsync;
#endif
                _hubConnection.StopAsync().HandleExceptions(); // TODO
                _stubRegistration.Dispose();
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

        private sealed class ClientCallStub : ICallStub
        {
            private readonly ClientEndPoint _endPoint;

            public ClientCallStub(ClientEndPoint endPoint)
            {
                Assert(endPoint != null);
                _endPoint = endPoint;
            }

            public async Task DeliverAsync(int seqNum, string base64) // byte[] bytes)
            {
                var minBytesLength = Base64Coder.ComputeBase64DecodedLength(base64.AsSpan());

                using (ArrayPool<byte>.Shared.Rent(minBytesLength, out var memory))
                {
                    var success = Base64Coder.TryFromBase64Chars(base64.AsSpan(), memory.Span, out var bytesWritten);
                    Assert(success);

                    memory = memory.Slice(start: 0, length: bytesWritten);

                    await _endPoint.ReceiveAsync(seqNum, memory);
                }
            }

            public Task AckAsync(int seqNum)
            {
                _endPoint.Ack(seqNum);
                return Task.CompletedTask;
            }
        }
    }

    // Based on: https://github.com/StephenCleary/AsyncEx/blob/db32fd5db0d1051e867b36ae039ea13d2c36eb91/src/Nito.AsyncEx.Coordination/AsyncManualResetEvent.cs
    internal sealed class AsyncConnectionLostEvent
    {
        /// <summary>
        /// The object used for synchronization.
        /// </summary>
        private readonly object _mutex = new object();

        /// <summary>
        /// The current state of the event.
        /// </summary>
        private TaskCompletionSource<object> _tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<object>();

        /// <summary>
        /// The semi-unique identifier for this instance. This is 0 if the id has not yet been created.
        /// </summary>
        private int _id;

        [DebuggerNonUserCode]
        private bool GetStateForDebugger => _tcs.Task.IsCompleted;

        #region C'tor

        /// <summary>
        /// Creates an async-compatible manual-reset event.
        /// </summary>
        /// <param name="set">Whether the manual-reset event is initially set or unset.</param>
        public AsyncConnectionLostEvent(bool set)
        {
            if (set)
            {
                _tcs.TrySetResult(null);
            }
        }

        /// <summary>
        /// Creates an async-compatible manual-reset event that is initially unset.
        /// </summary>
        public AsyncConnectionLostEvent() { }

        #endregion

        /// <summary>
        /// Whether this event is currently set. This member is seldom used; code using this member has a high possibility of race conditions.
        /// </summary>
        public bool IsSet
        {
            get
            {
                lock (_mutex)
                {
                    return _tcs.Task.IsCompleted;
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for this event to be set.
        /// </summary>
        public Task WaitAsync()
        {
            lock (_mutex)
            {
                return _tcs.Task;
            }
        }

        /// <summary>
        /// Asynchronously waits for this event to be set or for the wait to be canceled.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used to cancel the wait. If this token is already canceled, this method will first check whether the event is set.</param>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            var waitTask = WaitAsync();

            if (waitTask.IsCompleted)
            {
                return waitTask;
            }

            return waitTask.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Sets the event, atomically completing every task returned by <see cref="O:Nito.AsyncEx.AsyncManualResetEvent.WaitAsync"/>. 
        /// If the event is already set, this method does nothing.
        /// </summary>
        /// <returns>True if the event was set, false othewise.</returns>
        public bool Set()
        {
            bool result;

            lock (_mutex)
            {
                result = _tcs.TrySetResult(null);
            }

            return result;
        }

        /// <summary>
        /// Resets the event. If the event is already reset, this method does nothing.
        /// </summary>
        /// <returns>True if the event was reset, false otherwise.</returns>
        public bool Reset()
        {
            var result = false;

            lock (_mutex)
            {
                if (_tcs.Task.IsCompleted)
                {
                    result = true;
                    _tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<object>();
                }
            }

            return result;
        }
    }
}
