using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using AI4E.Routing;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Proxying;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugLogicalEndPoint : ILogicalEndPoint
    {
        private readonly DebugConnection _debugConnection;
        private readonly ILogger<DebugLogicalEndPoint> _logger;

        private readonly DisposableAsyncLazy<IProxy<LogicalEndPointSkeleton>> _proxyLazy;

        public DebugLogicalEndPoint(DebugConnection debugConnection, EndPointAddress endPoint, ILogger<DebugLogicalEndPoint> logger = null)
        {
            if (debugConnection == null)
                throw new ArgumentNullException(nameof(debugConnection));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            _debugConnection = debugConnection;
            EndPoint = endPoint;
            _logger = logger;

            _proxyLazy = new DisposableAsyncLazy<IProxy<LogicalEndPointSkeleton>>(
                factory: CreateProxyAsync,
                disposal: p => p.DisposeAsync().AsTask(), // TODO: This should accept a ValueTask
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        private async Task<IProxy<LogicalEndPointSkeleton>> CreateProxyAsync(CancellationToken cancellation)
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

            return proxy;
        }

        private Task<IProxy<LogicalEndPointSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            return _proxyLazy.Task.WithCancellation(cancellation);
        }

        public EndPointAddress EndPoint { get; }

        public async Task<ILogicalEndPointReceiveResult> ReceiveAsync(CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            var resultProxy = await proxy.ExecuteAsync(p => p.ReceiveAsync(cancellation));
            var resultValues = await resultProxy.ExecuteAsync(p => p.GetResultValues());

            return new DebugMessageReceiveResult(resultProxy, resultValues);
        }

        public async Task<(IMessage response, bool handled)> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            var buffer = new byte[message.Length];

            using (var stream = new MemoryStream(buffer, writable: true))
            {
                await message.WriteAsync(stream, cancellation);
            }

            var responseBuffer = await proxy.ExecuteAsync(p => p.SendAsync(buffer, remoteEndPoint, cancellation));
            var response = new Message();

            using (var stream = new MemoryStream(responseBuffer))
            {
                await response.ReadAsync(stream, cancellation);
            }

            bool handled;

            using (var stream = response.PopFrame().OpenStream())
            using (var reader = new BinaryReader(stream))
            {
                handled = reader.ReadBoolean();
            }

            response.Trim();

            return (response, handled);
        }

        #region Disposal

        public void Dispose()
        {
            _proxyLazy.Dispose();
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

            public async Task SendResponseAsync(IMessage response, bool handled)
            {
                var responseBuffer = response.ToArray();
                await _proxy.ExecuteAsync(p => p.SendResponseAsync(responseBuffer, handled));
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
