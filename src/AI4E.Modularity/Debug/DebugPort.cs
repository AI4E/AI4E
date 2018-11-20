/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using AI4E.Async;
using AI4E.Internal;
using AI4E.Modularity.Host;
using AI4E.Processing;
using AI4E.Proxying;
using AI4E.Remoting;
using AI4E.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugPort : IAsyncDisposable
    {
        #region Fields

        private readonly TcpListener _tcpHost;
        private readonly IAsyncProcess _connectionProcess;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<DebugPort> _logger;
        private readonly IRemoteMessageDispatcher _messageDispatcher;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncInitializationHelper<IPEndPoint> _initializationHelper;
        private readonly ConcurrentDictionary<IPEndPoint, DebugSession> _debugSessions = new ConcurrentDictionary<IPEndPoint, DebugSession>(new IPEndPointEqualityComparer());

        #endregion

        #region C'tor

        public DebugPort(IServiceProvider serviceProvider,
                         IAddressConversion<IPEndPoint> addressConversion,
                         IOptions<ModularityOptions> optionsAccessor,
                         IDateTimeProvider dateTimeProvider,
                         IRemoteMessageDispatcher messageDispatcher,
                         ILoggerFactory loggerFactory = null)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            var options = optionsAccessor.Value ?? new ModularityOptions();

            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _messageDispatcher = messageDispatcher;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger<DebugPort>();

            var endPoint = addressConversion.Parse(options.DebugConnection);

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
            // We MUST ensure that we only open the debug-port, if the handler for the debug messages are registered AND reached a globally consistent state (their routes are registered).
            await _messageDispatcher.WaitPendingRegistrationsAsync(cancellation);

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
                await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);
                await _connectionProcess.TerminateAsync().HandleExceptionsAsync(_logger);
            }
        }

        #endregion

        public IReadOnlyCollection<IDebugSession> DebugSessions => _debugSessions.Values.Where(p => p.IsMetadataSet).ToList();

        public void DebugSessionConnected(IPEndPoint address,
                                          EndPointAddress endPoint,
                                          ModuleIdentifier module,
                                          ModuleVersion moduleVersion)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (!_debugSessions.TryGetValue(address, out var session))
            {
                return; // TODO: Do we throw here?
            }

            // TODO: There is a race condition if this is hte metadata for an older debug session that had the same address coincidentally.
            session.SetMetadata(endPoint, module, moduleVersion);
            _logger?.LogTrace($"The metadata for debug session '{endPoint}' were set.");
        }

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

                    var debugSession = new DebugSession(this, client, _dateTimeProvider, _serviceProvider, _loggerFactory);

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

        private sealed class DebugSession : IDebugSession, IDisposable
        {
            private readonly DebugPort _debugServer;
            private readonly TcpClient _tcpClient;
            private readonly IDateTimeProvider _dateTimeProvider;
            private readonly IServiceProvider _serviceProvider;
            private readonly ILoggerFactory _loggerFactory;
            private readonly ILogger<DebugSession> _logger;
            private readonly DisposeAwareStream _stream;
            private readonly ProxyHost _rpcHost;

            public DebugSession(DebugPort debugServer,
                                TcpClient tcpClient,
                                IDateTimeProvider dateTimeProvider,
                                IServiceProvider serviceProvider,
                                ILoggerFactory loggerFactory)
            {
                if (debugServer == null)
                    throw new ArgumentNullException(nameof(debugServer));

                if (tcpClient == null)
                    throw new ArgumentNullException(nameof(tcpClient));

                if (dateTimeProvider == null)
                    throw new ArgumentNullException(nameof(dateTimeProvider));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                _debugServer = debugServer;
                _tcpClient = tcpClient;
                _dateTimeProvider = dateTimeProvider;
                _serviceProvider = serviceProvider;
                _loggerFactory = loggerFactory;

                _logger = _loggerFactory?.CreateLogger<DebugSession>();
                var streamLogger = _loggerFactory?.CreateLogger<DisposeAwareStream>();
                _stream = new DisposeAwareStream(_tcpClient.GetStream(), _dateTimeProvider, OnDebugStreamsCloses, streamLogger);
                _rpcHost = new ProxyHost(_stream, serviceProvider);
            }

            public IPEndPoint Address => _tcpClient.Client.RemoteEndPoint as IPEndPoint;

            private Task OnDebugStreamsCloses()
            {
                Dispose();
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                ExceptionHelper.HandleExceptions(() => _stream.Dispose(), _logger);
                ExceptionHelper.HandleExceptions(() => _rpcHost.Dispose(), _logger);
                ExceptionHelper.HandleExceptions(() =>
                {
                    if (!_debugServer._debugSessions.Remove(Address, this))
                    {
                        // TODO: Log failure. This should never be the case.
                    }
                }, _logger);
            }

            private readonly object _lock = new object();
            private volatile bool _isMetadataSet = false;

            public void SetMetadata(EndPointAddress endPoint, ModuleIdentifier module, ModuleVersion moduleVersion)
            {
                // Volatile read op.
                if (_isMetadataSet)
                    return;

                lock (_lock)
                {
                    // Volatile read op.
                    if (_isMetadataSet)
                        return;

                    EndPoint = endPoint;
                    Module = module;
                    ModuleVersion = moduleVersion;

                    // This must be set AFTER the actual metadata, because is is read from outside unsynchronized.
                    // Volatile protects us from the compiler, the runtime or the cpu reordering this operation with the operations above.
                    // Volatile write op.
                    _isMetadataSet = true;
                }
            }

            // Reading any of these props before MetadataSet returns true is UNSAFE and vulnerable to threading issues.
            public EndPointAddress EndPoint { get; private set; }
            public ModuleIdentifier Module { get; private set; }
            public ModuleVersion ModuleVersion { get; private set; }

            public bool IsMetadataSet => _isMetadataSet; // Volatile read op.
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
