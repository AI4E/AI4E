using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Coordination;
using AI4E.Internal;
using AI4E.Proxying;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugCoordinationManager : ICoordinationManager, IAsyncDisposable
    {
        private readonly ProxyHost _proxyHost;
        private readonly AsyncInitializationHelper<IProxy<CoordinationManagerSkeleton>> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private volatile string _session = null;

        public DebugCoordinationManager(ProxyHost proxyHost)
        {
            if (proxyHost == null)
                throw new ArgumentNullException(nameof(proxyHost));

            _proxyHost = proxyHost;
            _initializationHelper = new AsyncInitializationHelper<IProxy<CoordinationManagerSkeleton>>(
                async cancellation => await proxyHost.CreateAsync<CoordinationManagerSkeleton>(cancellation));
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        private Task<IProxy<CoordinationManagerSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation);
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

        public async ValueTask<IEntry> CreateAsync(string path, ReadOnlyMemory<byte> value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                if (!((await proxy.ExecuteAsync(p => p.CreateAsync(path, value.ToArray(), modes, cancelledOrDisposed))) is CoordinationManagerSkeleton.Entry entry))
                    return null;

                entry.SetCoordinationManagerStub(this, proxy);

                return entry;
            }
        }

        public async ValueTask<IEntry> GetOrCreateAsync(string path, ReadOnlyMemory<byte> value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                if (!((await proxy.ExecuteAsync(p => p.GetOrCreateAsync(path, value.ToArray(), modes, cancelledOrDisposed))) is CoordinationManagerSkeleton.Entry entry))
                    return null;

                entry.SetCoordinationManagerStub(this, proxy);

                return entry;
            }
        }

        public async ValueTask<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                if (!((await proxy.ExecuteAsync(p => p.GetAsync(path, cancelledOrDisposed))) is CoordinationManagerSkeleton.Entry entry))
                    return null;

                entry.SetCoordinationManagerStub(this, proxy);

                return entry;
            }
        }

        public async ValueTask<int> SetValueAsync(string path, ReadOnlyMemory<byte> value, int version = 0, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                return await proxy.ExecuteAsync(p => p.SetValueAsync(path, value.ToArray(), version, cancelledOrDisposed));
            }
        }

        public async ValueTask<int> DeleteAsync(string path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                return await proxy.ExecuteAsync(p => p.DeleteAsync(path, version, recursive, cancelledOrDisposed));
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
