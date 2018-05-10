using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Proxying;
using AI4E.Remoting;
using AI4E.Routing;
using static System.Diagnostics.Debug;

namespace AI4E.Debugging
{
    public sealed class DebugLogicalEndPoint : ILogicalEndPoint
    {
        private readonly ProxyHost _proxyHost;
        private readonly AsyncInitializationHelper<IProxy<LogicalEndPointSkeleton>> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public DebugLogicalEndPoint(ProxyHost proxyHost, EndPointRoute route)
        {
            if (proxyHost == null)
                throw new ArgumentNullException(nameof(proxyHost));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            _proxyHost = proxyHost;
            Route = route;

            _initializationHelper = new AsyncInitializationHelper<IProxy<LogicalEndPointSkeleton>>(
                async cancellation => await proxyHost.CreateAsync<LogicalEndPointSkeleton>(new object[] { route }, cancellation));
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        private Task<IProxy<LogicalEndPointSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation);
        }

        public EndPointRoute Route { get; }

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

        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, CancellationToken cancellation = default)
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
            var (success, proxy) = await _initializationHelper.CancelAsync().HandleExceptionsAsync();

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
