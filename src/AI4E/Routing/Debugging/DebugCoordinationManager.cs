using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Coordination;
using AI4E.Proxying;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.Debugging
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
                async cancellation => await proxyHost.ActivateAsync<CoordinationManagerSkeleton>(ActivationMode.Create, cancellation));
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

        public async Task<IEntry> CreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var proxy = await GetProxyAsync(cancellation);

                return await proxy.ExecuteAsync(p => p.CreateAsync(path, value, modes, cancellation));
            }
        }

        public async Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var proxy = await GetProxyAsync(cancellation);

                return await proxy.ExecuteAsync(p => p.GetOrCreateAsync(path, value, modes, cancellation));
            }
        }

        public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var proxy = await GetProxyAsync(cancellation);

                return await proxy.ExecuteAsync(p => p.GetAsync(path, cancellation));
            }
        }

        public async Task<int> SetValueAsync(string path, byte[] value, int version = 0, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var proxy = await GetProxyAsync(cancellation);

                return await proxy.ExecuteAsync(p => p.SetValueAsync(path, value, version, cancellation));
            }
        }

        public async Task<int> DeleteAsync(string path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var proxy = await GetProxyAsync(cancellation);

                return await proxy.ExecuteAsync(p => p.DeleteAsync(path, version, recursive, cancellation));
            }
        }

        public async Task<string> GetSessionAsync(CancellationToken cancellation = default)
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

                var proxy = await GetProxyAsync(cancellation);

                session = await proxy.ExecuteAsync(p => p.GetSessionAsync(cancellation));

                Interlocked.CompareExchange(ref _session, session, null);

                return session;
            }
        }
    }
}
