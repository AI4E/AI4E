using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Proxying;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugCoordinationManager : ICoordinationManager, IAsyncDisposable
    {
        private readonly AsyncInitializationHelper<(ProxyHost proxyHost, IProxy<CoordinationManagerSkeleton> proxy)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly DebugConnection _debugConnection;
        private readonly ILogger<DebugCoordinationManager> _logger;
        private volatile string _session = null;

        public DebugCoordinationManager(DebugConnection debugConnection, ILogger<DebugCoordinationManager> logger = null)
        {
            if (debugConnection == null)
                throw new ArgumentNullException(nameof(debugConnection));

            _initializationHelper = new AsyncInitializationHelper<(ProxyHost proxyHost, IProxy<CoordinationManagerSkeleton> proxy)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _debugConnection = debugConnection;
            _logger = logger;
        }

        private async Task<(ProxyHost proxyHost, IProxy<CoordinationManagerSkeleton> proxy)> InitializeInternalAsync(CancellationToken cancellation)
        {
            ProxyHost proxyHost = null;
            IProxy<CoordinationManagerSkeleton> proxy;
            try
            {
                proxyHost = await _debugConnection.GetProxyHostAsync(cancellation);
                proxy = await proxyHost.CreateAsync<CoordinationManagerSkeleton>(cancellation);
            }
            catch (OperationCanceledException)
            {
                proxyHost?.Dispose();
                throw;
            }

            return (proxyHost, proxy);
        }

        private async ValueTask<IProxy<CoordinationManagerSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            var (_, proxy) = await _initializationHelper.Initialization.WithCancellation(cancellation);
            return proxy;
        }

        private async ValueTask<ProxyHost> GetProxyHostAsync(CancellationToken cancellation)
        {
            var (proxyHost, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);
            return proxyHost;
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
            var (success, (proxyHost, proxy)) = await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);

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

        public async ValueTask<IEntry> CreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                if (!((await proxy.ExecuteAsync(p => p.CreateAsync(path.EscapedPath.ConvertToString(), value.ToArray(), modes, cancelledOrDisposed))) is CoordinationManagerSkeleton.Entry entry))
                    return null;

                entry.SetCoordinationManagerStub(this, proxy);

                return entry;
            }
        }

        public async ValueTask<IEntry> GetOrCreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                if (!((await proxy.ExecuteAsync(p => p.GetOrCreateAsync(path.EscapedPath.ConvertToString(), value.ToArray(), modes, cancelledOrDisposed))) is CoordinationManagerSkeleton.Entry entry))
                    return null;

                entry.SetCoordinationManagerStub(this, proxy);

                return entry;
            }
        }

        public async ValueTask<IEntry> GetAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                if (!((await proxy.ExecuteAsync(p => p.GetAsync(path.EscapedPath.ConvertToString(), cancelledOrDisposed))) is CoordinationManagerSkeleton.Entry entry))
                    return null;

                entry.SetCoordinationManagerStub(this, proxy);

                return entry;
            }
        }

        public async ValueTask<int> SetValueAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, int version = 0, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                return await proxy.ExecuteAsync(p => p.SetValueAsync(path.EscapedPath.ConvertToString(), value.ToArray(), version, cancelledOrDisposed));
            }
        }

        public async ValueTask<int> DeleteAsync(CoordinationEntryPath path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                return await proxy.ExecuteAsync(p => p.DeleteAsync(path.EscapedPath.ConvertToString(), version, recursive, cancelledOrDisposed));
            }
        }

        public async ValueTask<string> GetSessionAsync(CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                // Once determined, the session is a constant and can be cached.
                // Be aware that this can change in the future, 
                // for example when implementing reconnection on session termination.

                var session = _session; // Volatile read op.

                if (session != null)
                    return session;

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                session = await proxy.ExecuteAsync(p => p.GetSessionAsync(cancelledOrDisposed));

                Interlocked.CompareExchange(ref _session, session, null);

                return session;
            }
        }
    }
}
