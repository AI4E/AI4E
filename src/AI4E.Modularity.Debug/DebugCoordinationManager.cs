/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Coordination;
using AI4E.Utils.Proxying;
using AI4E.Utils;
using AI4E.Utils.Memory;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugCoordinationManager : ICoordinationManager
    {
        private readonly DebugConnection _debugConnection;
        private readonly ILogger<DebugCoordinationManager> _logger;

        private readonly DisposableAsyncLazy<IProxy<CoordinationManagerSkeleton>> _proxyLazy;
        private readonly DisposableAsyncLazy<Session> _sessionLazy;

        public DebugCoordinationManager(DebugConnection debugConnection, ILogger<DebugCoordinationManager> logger = null)
        {
            if (debugConnection == null)
                throw new ArgumentNullException(nameof(debugConnection));

            _debugConnection = debugConnection;
            _logger = logger;

            _proxyLazy = new DisposableAsyncLazy<IProxy<CoordinationManagerSkeleton>>(
                factory: CreateProxyAsync,
                disposal: p => p.DisposeAsync().AsTask(), // TODO: This should accept a ValueTask
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);

            _sessionLazy = new DisposableAsyncLazy<Session>(
                factory: GetSessionInternalAsync,
                options: DisposableAsyncLazyOptions.ExecuteOnCallingThread | DisposableAsyncLazyOptions.RetryOnFailure);
        }

        private async Task<IProxy<CoordinationManagerSkeleton>> CreateProxyAsync(CancellationToken cancellation)
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

            return proxy;
        }

        private Task<IProxy<CoordinationManagerSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            return _proxyLazy.Task.WithCancellation(cancellation);
        }

        #region Disposal

        public void Dispose()
        {
            _proxyLazy.Dispose();
            _sessionLazy.Dispose();
        }

        #endregion

        public async ValueTask<IEntry> CreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            if (!(await proxy.ExecuteAsync(p => p.CreateAsync(path.EscapedPath.ConvertToString(), value.ToArray(), modes, cancellation)) is CoordinationManagerSkeleton.Entry entry))
                return null;

            entry.SetCoordinationManagerStub(this, proxy);

            return entry;
        }

        public async ValueTask<IEntry> GetOrCreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            if (!(await proxy.ExecuteAsync(p => p.GetOrCreateAsync(path.EscapedPath.ConvertToString(), value.ToArray(), modes, cancellation)) is CoordinationManagerSkeleton.Entry entry))
                return null;

            entry.SetCoordinationManagerStub(this, proxy);

            return entry;
        }

        public async ValueTask<IEntry> GetAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            if (!(await proxy.ExecuteAsync(p => p.GetAsync(path.EscapedPath.ConvertToString(), cancellation)) is CoordinationManagerSkeleton.Entry entry))
                return null;

            entry.SetCoordinationManagerStub(this, proxy);

            return entry;
        }

        public async ValueTask<int> SetValueAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, int version = 0, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            return await proxy.ExecuteAsync(p => p.SetValueAsync(path.EscapedPath.ConvertToString(), value.ToArray(), version, cancellation));
        }

        public async ValueTask<int> DeleteAsync(CoordinationEntryPath path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
        {
            var proxy = await GetProxyAsync(cancellation);

            return await proxy.ExecuteAsync(p => p.DeleteAsync(path.EscapedPath.ConvertToString(), version, recursive, cancellation));
        }

        private async Task<Session> GetSessionInternalAsync(CancellationToken cancellation)
        {
            var proxy = await GetProxyAsync(cancellation);
            var session = await proxy.ExecuteAsync(p => p.GetSessionAsync(cancellation));
            return Session.FromChars(session.AsSpan());
        }

        public ValueTask<Session> GetSessionAsync(CancellationToken cancellation = default)
        {
            // Once determined, the session is a constant and can be cached.
            // Be aware that this can change in the future, 
            // for example when implementing reconnection on session termination.
            return new ValueTask<Session>(_sessionLazy.Task.WithCancellation(cancellation));
        }
    }
}
