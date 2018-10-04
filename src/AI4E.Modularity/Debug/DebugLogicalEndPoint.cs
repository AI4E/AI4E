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

            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

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

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            byte[] buffer;

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    buffer = await proxy.ExecuteAsync(p => p.ReceiveAsync(combinedCancellation));
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            var message = new Message();

            using (var stream = new MemoryStream(buffer))
            {
                await message.ReadAsync(stream, cancellation);
            }

            return message;
        }

        public async Task SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default)
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
                    await proxy.ExecuteAsync(p => p.SendAsync(buffer, remoteEndPoint, combinedCancellation));
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            var responseBuffer = new byte[response.Length];
            var requestBuffer = new byte[request.Length];

            using (var stream = new MemoryStream(responseBuffer, writable: true))
            {
                await response.WriteAsync(stream, cancellation);
            }

            using (var stream = new MemoryStream(requestBuffer, writable: true))
            {
                await request.WriteAsync(stream, cancellation);
            }

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    await proxy.ExecuteAsync(p => p.SendAsync(responseBuffer, requestBuffer, combinedCancellation));
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
    }
}
