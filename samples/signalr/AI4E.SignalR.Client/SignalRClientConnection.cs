using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.SignalR.Client
{
    public sealed class SignalRClientConnection : IPersistentConnection, IAsyncDisposable
    {
        private readonly ILogger<SignalRClientConnection> _logger;
        private readonly HubConnection _connection;
        private readonly Func<Exception, Task> _connectionClosedHandler;
        private readonly ClientSkeleton _client;
        private readonly IDisposable _registrations;

        private AsyncProcess _connectionProcess;
        private readonly AsyncInitializationHelper<(string clientId, string securityToken)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncManualResetEvent _connectionLostEvent = new AsyncManualResetEvent();
        private volatile ConnectResponse _connectResponse;
        private int _nextSeqNum = 0;

        public SignalRClientConnection(HubConnection connection, ILogger<SignalRClientConnection> logger = null)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            _connection = connection;
            _connectionClosedHandler = ConnectionClosed;
            _connection.Closed += _connectionClosedHandler;

            _client = new ClientSkeleton(this);
            _registrations = _connection.Register(_client);

            _initializationHelper = new AsyncInitializationHelper<(string clientId, string securityToken)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _logger = logger;
        }

        public async Task<string> GetIdAsync(CancellationToken cancellation)
        {
            var (clientId, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return clientId;
        }

        private async Task<string> GetSecurityTokenAsync(CancellationToken cancellation)
        {
            var (_, securityToken) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return securityToken;
        }

        private Task ConnectionClosed(Exception exc)
        {
            return Task.CompletedTask;
        }

        public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }

        public Task SendAsync(CancellationToken cancellation)
        {
            throw new NotImplementedException();
        }

        #region Initialization

        private async Task<(string clientId, string securityToken)> InitializeInternalAsync(CancellationToken cancellation)
        {
            var clientIdSupplier = new TaskCompletionSource<(string clientId, string securityToken)>();
            _connectionProcess = new AsyncProcess(c => ConnectionProcess(clientIdSupplier, c));

            // Await connection process startup
            await _connectionProcess.StartAsync(cancellation);

            // Await first connection
            return await clientIdSupplier.Task.WithCancellation(cancellation);
        }

        #endregion

        #region Connection / Reconnection

        private async Task ConnectionProcess(TaskCompletionSource<(string clientId, string securityToken)> clientIdSupplier, CancellationToken cancellation)
        {
            try
            {
                var clientId = default(string);
                var securityToken = default(string);
                var firstConnectionEstablished = false;

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        // If we lost connection, we have to establish the underlying signalR connection first.
                        _connectionLostEvent.Reset();
                        await EstablishSignalRConnectionAsync(cancellation);

                        try
                        {
                            if (!firstConnectionEstablished)
                            {
                                (clientId, securityToken) = await ConnectAsync(cancellation);

                                Assert(clientId != null);

                                clientIdSupplier.SetResult((clientId, securityToken));
                                firstConnectionEstablished = true;
                            }
                            else
                            {
                                Assert(clientId != null);
                                await ReconnectAsync(clientId, securityToken, cancellation);
                            }
                        }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                        catch (ConnectionRejectedException exc)
                        {
                            // If the connection is rejected, the connection will never get to a state that a clean transmission is possible, so we mark it as closed.
                            _logger?.LogError(exc, "The server rejected the connection.");
                            Dispose();
                            Assert(cancellation.IsCancellationRequested);

                            // We can safely return from the process as it will terminate anyway.
                            return;
                        }
                        catch (Exception) // TODO: What exception is thrown if the signalr connection is closed while sending a request?
                        {
                            // An exception occured while performing the (re)connect handshake. We try it again be first establishing a new signalr connection etc.
                            continue;
                        }

                        // Wait till the connection is lost
                        await _connectionLostEvent.WaitAsync(cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception exc)
                    {
                        _logger?.LogDebug(exc, "Failure connecting to server.");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                clientIdSupplier.TrySetCanceled(cancellation);
                throw;
            }
            catch (OperationCanceledException)
            {
                clientIdSupplier.TrySetCanceled();
                throw;
            }
        }

        private async Task EstablishSignalRConnectionAsync(CancellationToken cancellation)
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
                    await _connection.StartAsync(cancellation);
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

        private async Task<(string clientId, string securityToken)> ConnectAsync(CancellationToken cancellation)
        {
            var clientId = GenerateClientId();

            Task RequestOperation(int seqNum)
            {
                return _connection.InvokeAsync<ISignalRServer>(p => p.ConnectAsync(seqNum, clientId));
            }

            var rejectReason = RejectReason.Unknown;

            while (cancellation.ThrowOrContinue())
            {
                bool success;
                string securityToken;

                (success, rejectReason, securityToken) = await RequestHelper.PerformRequestAsync<(bool success, RejectReason rejectReason, string securityToken)>(RequestOperation, CancelRequestAsync, RegisterConnectResponseAsync, GetNextSeqNum, cancellation);

                if (success)
                {
                    return (clientId, securityToken);
                }

                if (rejectReason != RejectReason.IdAlreadyAssigned)
                {
                    break;
                }

                clientId = GenerateClientId();
            }

            throw new ConnectionRejectedException(rejectReason);
        }

        private async Task ReconnectAsync(string clientId, string securityToken, CancellationToken cancellation)
        {
            Task RequestOperation(int seqNum)
            {
                return _connection.InvokeAsync<ISignalRServer>(p => p.ReconnectAsync(seqNum, clientId, securityToken));
            }

            var (success, rejectReason, _) = await RequestHelper.PerformRequestAsync<(bool success, RejectReason rejectReason, string securityToken)>(RequestOperation, CancelRequestAsync, RegisterConnectResponseAsync, GetNextSeqNum, cancellation);

            if (success)
            {
                return;
            }

            throw new ConnectionRejectedException(rejectReason);
        }

        private IDisposable RegisterConnectResponseAsync(int seqNum, TaskCompletionSource<(bool success, RejectReason rejectReason, string securityToken)> response)
        {
            var connectResponse = new ConnectResponse(this, seqNum, response);

            Interlocked.Exchange(ref _connectResponse, connectResponse)?.Dispose();

            return connectResponse;
        }

        private string GenerateClientId()
        {
            return Guid.NewGuid().ToString();
        }

        #endregion

        private Task CancelRequestAsync(int seqNum)
        {
            return _connection.InvokeAsync<ISignalRServer>(p => p.CancelAsync(GetNextSeqNum(), seqNum));
        }

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

        }

        #endregion

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        private sealed class ConnectResponse : IDisposable
        {
            private readonly SignalRClientConnection _owner;

            public ConnectResponse(SignalRClientConnection owner,
                                   int seqNum,
                                   TaskCompletionSource<(bool success, RejectReason rejectReason, string securityToken)> response)
            {
                Assert(owner != null);
                Assert(response != null);

                _owner = owner;
                SeqNum = seqNum;
                Response = response;
            }

            public int SeqNum { get; }
            public TaskCompletionSource<(bool success, RejectReason rejectReason, string securityToken)> Response { get; }

            public void Reject(RejectReason reason)
            {
                if (reason == RejectReason.Canceled)
                {
                    Response.TrySetCanceled();
                }
                else
                {
                    Response.TrySetResult((success: false, rejectReason: reason, securityToken: default));
                }
            }

            public void Accept(string securityToken)
            {
                Response.TrySetResult((success: true, rejectReason: default, securityToken));
            }

            public void Dispose()
            {
                Response.TrySetCanceled();
                Interlocked.CompareExchange(ref _owner._connectResponse, null, this);
            }
        }

        private sealed class ClientSkeleton : ISignalRClient
        {
            private readonly SignalRClientConnection _connection;

            public ClientSkeleton(SignalRClientConnection connection)
            {
                Assert(connection != null);
                _connection = connection;
            }

            public Task AcceptAsync(int corr, string securityToken)
            {
                var connectResponse = _connection._connectResponse; // Volatile read op.

                if (connectResponse.SeqNum == corr)
                {
                    connectResponse.Accept(securityToken);
                }

                return Task.CompletedTask;
            }

            public Task RejectAsync(int corr, RejectReason reason)
            {
                var connectResponse = _connection._connectResponse; // Volatile read op.

                if (connectResponse.SeqNum == corr)
                {
                    connectResponse.Reject(reason);
                }

                return Task.CompletedTask;
            }

            public Task DisconnectAsync(int seqNum, DisconnectReason reason)
            {
                throw new NotImplementedException();
            }

            public Task DisconnectedAsync(int seqNum)
            {
                throw new NotImplementedException();
            }

            public Task DeliverAsync(int seqNum, byte[] payload)
            {
                throw new NotImplementedException();
            }

            public Task CancelAsync(int seqNum, int corr)
            {
                throw new NotImplementedException();
            }

            public Task CancelledAsync(int seqNum, int corr)
            {
                throw new NotImplementedException();
            }
        }
    }
}
