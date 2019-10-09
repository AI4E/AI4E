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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Utils.Async;
using AI4E.Utils.Proxying;
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

        private class CoordinationManagerSkeleton : IDisposable
        {
            private readonly ICoordinationManager _coordinationManager;

            public CoordinationManagerSkeleton(ICoordinationManagerFactory coordinationManagerFactory)
            {
                if (coordinationManagerFactory == null)
                    throw new ArgumentNullException(nameof(coordinationManagerFactory));

                _coordinationManager = coordinationManagerFactory.CreateCoordinationManager();

                System.Diagnostics.Debug.Assert(_coordinationManager != null);
            }

            public async Task<IEntry> CreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
            {
                var result = await _coordinationManager.CreateAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), value, modes, cancellation);

                if (result == null)
                    return null;

                return new Entry(ProxyHost.CreateProxy(this), result);
            }

            public async Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
            {
                var result = await _coordinationManager.GetOrCreateAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), value, modes, cancellation);

                if (result == null)
                    return null;

                return new Entry(ProxyHost.CreateProxy(this), result);
            }

            public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
            {
                var result = await _coordinationManager.GetAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), cancellation);

                if (result == null)
                    return null;

                return new Entry(ProxyHost.CreateProxy(this), result);
            }

            public Task<int> SetValueAsync(string path, byte[] value, int version = 0, CancellationToken cancellation = default)
            {
                return _coordinationManager.SetValueAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), value, version, cancellation).AsTask();
            }

            public Task<int> DeleteAsync(string path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
            {
                return _coordinationManager.DeleteAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), version, recursive, cancellation).AsTask();
            }

            public async Task<string> GetSessionAsync(CancellationToken cancellation = default)
            {
                return (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
            }

            #region Disposal

            public void Dispose()
            {
                _coordinationManager.Dispose();
            }

            #endregion

            [Serializable]
            internal /*private*/ sealed class Entry : IEntry
            {
                [NonSerialized] // TODO: Proxy is currently not serializable. This is a bug in the proxying infrastructure
                private /*readonly*/ IProxy<CoordinationManagerSkeleton> _proxy;

                [NonSerialized]
                private ICoordinationManager _coordinationManager;

                public ICoordinationManager CoordinationManager => _coordinationManager;

                internal void SetCoordinationManagerStub(ICoordinationManager coordinationManager, IProxy<CoordinationManagerSkeleton> proxy)
                {
                    _coordinationManager = coordinationManager;
                    _proxy = proxy;
                }

                private readonly string[] _children; // Escaped child names
                private readonly byte[] _value;
                private readonly string _path; // Escaped path

                public Entry(IProxy<CoordinationManagerSkeleton> proxy, IEntry entry)
                {
                    System.Diagnostics.Debug.Assert(proxy != null);
                    System.Diagnostics.Debug.Assert(entry != null);

                    _proxy = proxy;
                    _path = entry.Path.EscapedPath.ConvertToString();
                    _value = entry.Value.ToArray();
                    _children = entry.Children.Select(p => p.EscapedSegment.ConvertToString()).ToArray();

                    Version = entry.Version;
                    CreationTime = entry.CreationTime;
                    LastWriteTime = entry.LastWriteTime;
                }

                public CoordinationEntryPath Path => CoordinationEntryPath.FromEscapedPath(_path.AsMemory());

                public int Version { get; }

                public DateTime CreationTime { get; }

                public DateTime LastWriteTime { get; }

                public ReadOnlyMemory<byte> Value => _value;

                public IReadOnlyList<CoordinationEntryPathSegment> Children => _children.Select(p => CoordinationEntryPathSegment.FromEscapedSegment(p.AsMemory())).ToImmutableList();

                public CoordinationEntryPathSegment Name => Path.Segments.LastOrDefault();

                public CoordinationEntryPath ParentPath => Path.GetParentPath();
            }
        }
    }
}
