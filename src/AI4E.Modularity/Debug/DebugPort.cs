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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Modularity.Host;
using AI4E.Routing;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using AI4E.Utils.Proxying;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugPort : IAsyncDisposable
    {
        #region Fields

        private readonly TcpListener _tcpHost;
        private readonly AsyncProcess _connectionProcess;
        private readonly IServiceProvider _serviceProvider;
        private readonly IRunningModuleManager _runningModuleManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DebugPort> _logger;
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncInitializationHelper<IPEndPoint> _initializationHelper;
        private readonly ConcurrentDictionary<IPEndPoint, DebugSession> _debugSessions = new ConcurrentDictionary<IPEndPoint, DebugSession>(new IPEndPointEqualityComparer());

        #endregion

        #region C'tor

        public DebugPort(IServiceProvider serviceProvider,
                         IRunningModuleManager runningModuleManager,
                         IOptions<ModularityOptions> optionsAccessor,
                         IRemoteMessageDispatcher messageDispatcher,
                         ILoggerFactory loggerFactory = null)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (runningModuleManager == null)
                throw new ArgumentNullException(nameof(runningModuleManager));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            var options = optionsAccessor.Value ?? new ModularityOptions();

            _serviceProvider = serviceProvider;
            _runningModuleManager = runningModuleManager;
            _messageDispatcher = messageDispatcher;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger<DebugPort>();

            var endPoint = IPEndPointConverter.AddressFromString(options.DebugConnection);

            _tcpHost = new TcpListener(endPoint);
            _connectionProcess = new AsyncProcess(ConnectProcedure);
            _initializationHelper = new AsyncInitializationHelper<IPEndPoint>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        public ValueTask<IPEndPoint> GetLocalAddressAsync(CancellationToken cancellation)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation).AsValueTask();
        }

        #region Initialization

        private async Task<IPEndPoint> InitializeInternalAsync(CancellationToken cancellation)
        {
            // We MUST ensure that we only open the debug-port if
            // - the handler for the debug messages is registered AND
            // - reached a globally consistent state (its routes are registered).

            if (_messageDispatcher is IAsyncInitialization asyncInitialization)
            {
                await asyncInitialization.Initialization.WithCancellation(cancellation);
            }

            _tcpHost.Start();
            var localAddress = (IPEndPoint)_tcpHost.Server.LocalEndPoint;
            Assert(localAddress != null);

            await _connectionProcess.StartAsync(cancellation);

            return localAddress;
        }

        #endregion

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
            try
            {
                _tcpHost.Stop();
            }
            finally
            {
                await _initializationHelper.CancelAsync().HandleExceptionsAsync(logger: _logger);
                await _connectionProcess.TerminateAsync().HandleExceptionsAsync(_logger);
            }
        }

        #endregion

        // TODO: We should guarantee that there are no two debug modules that are equal. We can omit the Distinct call here.
        public IReadOnlyCollection<DebugModuleProperties> ConnectedDebugModules =>
            _debugSessions.Values.Where(p => p.IsMetadataSet).Select(p => p.ModuleProperties).Distinct().ToList();

        private async Task ConnectProcedure(CancellationToken cancellation)
        {
            await _initializationHelper.Initialization;

            _logger?.LogTrace("Started listening for debug connections.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var client = await _tcpHost.AcceptTcpClientAsync().WithCancellation(cancellation);
                    _logger?.LogInformation($"Debug connection established for ip end-point '{(client.Client.RemoteEndPoint as IPEndPoint).ToString()}'.");

                    var debugSession = new DebugSession(this, client, _serviceProvider, _loggerFactory);

                    if (!_debugSessions.TryAdd(debugSession.Address, debugSession))
                    {
                        // TODO: Log failure

                        debugSession.Dispose();
                    }
                }
                catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { return; }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, "An exception occured while handling a debug connection attempt.");
                }
            }
        }

        private sealed class DebugSession : IDisposable
        {
            private readonly DebugPort _debugServer;
            private readonly TcpClient _tcpClient;
            private readonly IServiceProvider _serviceProvider;
            private readonly ILoggerFactory _loggerFactory;
            private readonly ILogger<DebugSession> _logger;
            private readonly DisposeAwareStream _stream;

            private readonly DisposableAsyncLazy<DebugModuleProperties> _propertiesLazy;
            private readonly DisposableAsyncLazy<ProxyHost> _proxyHostLazy;

            public DebugSession(DebugPort debugServer,
                                TcpClient tcpClient,
                                IServiceProvider serviceProvider,
                                ILoggerFactory loggerFactory)
            {
                if (debugServer == null)
                    throw new ArgumentNullException(nameof(debugServer));

                if (tcpClient == null)
                    throw new ArgumentNullException(nameof(tcpClient));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                _debugServer = debugServer;
                _tcpClient = tcpClient;
                Address = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                _serviceProvider = serviceProvider;
                _loggerFactory = loggerFactory;

                _logger = _loggerFactory?.CreateLogger<DebugSession>();
                var streamLogger = _loggerFactory?.CreateLogger<DisposeAwareStream>();
                _stream = new DisposeAwareStream(_tcpClient.GetStream(), OnDebugStreamsCloses, streamLogger);

                _propertiesLazy = new DisposableAsyncLazy<DebugModuleProperties>(
                    factory: CreatePropertiesAsync,
                    disposal: DisposePropertiesAsync,
                    options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);

                _proxyHostLazy = new DisposableAsyncLazy<ProxyHost>(
                    factory: CreateProxyHostAsync,
                    disposal: proxyHost => proxyHost.DisposeAsync(),
                    options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
            }

            private async Task<DebugModuleProperties> CreatePropertiesAsync(CancellationToken cancellation)
            {
                var properties = await DebugModuleProperties.ReadAsync(_stream, cancellation);

                // TODO: https://github.com/AI4E/AI4E/issues/102
                //       The messaging system does not guarantee message ordering.
                //       The message may be delivered AFTER a ModuleTerminated message for the same module that was sent thereafter.
                _debugServer._runningModuleManager.Started(properties.Module);

                return properties;
            }

            private Task DisposePropertiesAsync(DebugModuleProperties properties)
            {
                _debugServer._runningModuleManager.Terminated(properties.Module);

                return Task.CompletedTask;
            }

            private async Task<ProxyHost> CreateProxyHostAsync(CancellationToken cancellation)
            {
                // We have to wait until the properties are read from the stream.
                await _propertiesLazy.Task.WithCancellation(cancellation);

                return new ProxyHost(_stream, _serviceProvider);
            }

            // Do not lazily lookup this from _tcpClient, as we need this in the disposal, but then, the _tcpClient is already disposed.
            public IPEndPoint Address { get; }

            private Task OnDebugStreamsCloses()
            {
                Dispose();
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                ExceptionHelper.HandleExceptions(() => _stream.Dispose(), _logger);

                _proxyHostLazy.Dispose();
                _propertiesLazy.Dispose();

                ExceptionHelper.HandleExceptions(() =>
                {
                    if (!_debugServer._debugSessions.Remove(Address, this))
                    {
                        // TODO: Log failure. This should never be the case.
                    }
                }, _logger);
            }

            public Task<DebugModuleProperties> GetModulePropertiesAsync(CancellationToken cancellation)
            {
                return _propertiesLazy.Task.WithCancellation(cancellation);
            }

            // TODO: Rename
            public bool IsMetadataSet => _propertiesLazy.IsStarted && _propertiesLazy.Task.IsCompleted;

            public DebugModuleProperties ModuleProperties => _propertiesLazy.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private sealed class IPEndPointEqualityComparer : IEqualityComparer<IPEndPoint>
        {
            public bool Equals(IPEndPoint x, IPEndPoint y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(IPEndPoint obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
