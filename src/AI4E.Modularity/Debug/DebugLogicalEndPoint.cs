using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Proxying;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugLogicalEndPoint : ILogicalEndPoint
    {
        private readonly AsyncInitializationHelper<(ProxyHost proxyHost, IProxy<LogicalEndPointSkeleton> proxy)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly DebugConnection _debugConnection;
        private readonly ILogger<DebugLogicalEndPoint> _logger;

        public DebugLogicalEndPoint(DebugConnection debugConnection, EndPointAddress endPoint, ILogger<DebugLogicalEndPoint> logger = null)
        {
            if (debugConnection == null)
                throw new ArgumentNullException(nameof(debugConnection));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            _debugConnection = debugConnection;
            EndPoint = endPoint;
            _logger = logger;
            _initializationHelper = new AsyncInitializationHelper<(ProxyHost proxyHost, IProxy<LogicalEndPointSkeleton> proxy)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        private async Task<(ProxyHost proxyHost, IProxy<LogicalEndPointSkeleton> proxy)> InitializeInternalAsync(CancellationToken cancellation)
        {
            ProxyHost proxyHost = null;
            IProxy<LogicalEndPointSkeleton> proxy;
            try
            {
                proxyHost = await _debugConnection.GetProxyHostAsync(cancellation);
                proxy = await proxyHost.CreateAsync<LogicalEndPointSkeleton>(new object[] { EndPoint }, cancellation);
            }
            catch (OperationCanceledException)
            {
                proxyHost?.Dispose();
                throw;
            }

            return (proxyHost, proxy);
        }

        private async ValueTask<ProxyHost> GetProxyHostAsync(CancellationToken cancellation)
        {
            var (proxyHost, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);
            return proxyHost;
        }

        private async ValueTask<IProxy<LogicalEndPointSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            var (_, proxy) = await _initializationHelper.Initialization.WithCancellation(cancellation);
            return proxy;
        }

        public EndPointAddress EndPoint { get; }

        public async Task<ILogicalEndPointReceiveResult> ReceiveAsync(CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var resultProxy = await proxy.ExecuteAsync(p => p.ReceiveAsync(combinedCancellation));
                    var resultValues = await resultProxy.ExecuteAsync(p => p.GetResultValues());

                    return new DebugMessageReceiveResult(resultProxy, resultValues);
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            var buffer = new byte[message.Length];

            using (var stream = new MemoryStream(buffer, writable: true))
            {
                await message.WriteAsync(stream, cancellation);
            }

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var responseBuffer = await proxy.ExecuteAsync(p => p.SendAsync(buffer, remoteEndPoint, combinedCancellation));
                    var response = new Message();

                    using (var stream = new MemoryStream(buffer))
                    {
                        await response.ReadAsync(stream, cancellation);
                    }

                    return response;
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public Task Initialization => _initializationHelper.Initialization;

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
            var (success, (_, proxy)) = await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);

            if (success)
            {
                Assert(proxy != null);

                await proxy.DisposeAsync();
            }
        }

        private void CheckDisposal()
        {
            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        #endregion

        private sealed class DebugMessageReceiveResult : ILogicalEndPointReceiveResult
        {
            private readonly IProxy<MessageReceiveResultSkeleton> _proxy;
            private readonly MessageReceiveResultValues _values;

            public DebugMessageReceiveResult(IProxy<MessageReceiveResultSkeleton> proxy, MessageReceiveResultValues values)
            {
                _proxy = proxy;
                _values = values;
            }

            public EndPointAddress RemoteEndPoint => _values.RemoteEndPoint;

            public CancellationToken Cancellation => _values.Cancellation;

            public IMessage Message
            {
                get
                {
                    var message = new Message();

                    using (var stream = new MemoryStream(_values.Message))
                    {
                        message.ReadAsync(stream, cancellation: default).ConfigureAwait(false).GetAwaiter().GetResult();
                    }

                    return message;
                }
            }

            Packet<EndPointAddress> IMessageReceiveResult<Packet<EndPointAddress>>.Packet
                => new Packet<EndPointAddress>(Message, RemoteEndPoint);

            public async Task SendResponseAsync(IMessage response)
            {
                var responseBuffer = response.ToArray();
                await _proxy.ExecuteAsync(p => p.SendResponseAsync(responseBuffer));
            }

            public async Task SendCancellationAsync()
            {
                await _proxy.ExecuteAsync(p => p.SendCancellationAsync());
            }

            public async Task SendAckAsync()
            {
                await _proxy.ExecuteAsync(p => p.SendAckAsync());
            }

            public void Dispose()
            {
                _proxy.ExecuteAsync(p => p.Dispose()).HandleExceptions();
            }
        }
    }
}
