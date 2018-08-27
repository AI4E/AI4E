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
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly ConcurrentDictionary<DebugSession, byte> _debugSessions = new ConcurrentDictionary<DebugSession, byte>();

        #endregion

        #region C'tor

        public DebugPort(IServiceProvider serviceProvider,
                         IAddressConversion<IPEndPoint> addressConversion,
                         IOptions<ModularityOptions> optionsAccessor,
                         IDateTimeProvider dateTimeProvider,
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

            var options = optionsAccessor.Value ?? new ModularityOptions();

            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory?.CreateLogger<DebugPort>();
            var endPoint = addressConversion.Parse(options.DebugConnection);

            _tcpHost = new TcpListener(endPoint);
            _tcpHost.Start();
            LocalAddress = (IPEndPoint)_tcpHost.Server.LocalEndPoint;
            Assert(LocalAddress != null);

            _connectionProcess = new AsyncProcess(ConnectProcedure);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        public IPEndPoint LocalAddress { get; }

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _connectionProcess.StartAsync(cancellation);
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

        private async Task ConnectProcedure(CancellationToken cancellation)
        {
            _logger?.LogTrace("Started listening for debug connections.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var client = await _tcpHost.AcceptTcpClientAsync().WithCancellation(cancellation);
                    _logger?.LogInformation($"Debug connection established for ip end-point '{(client.Client.RemoteEndPoint as IPEndPoint).ToString()}'.");

                    _debugSessions.TryAdd(new DebugSession(this, client, _dateTimeProvider, _serviceProvider, _loggerFactory), 0);
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
            private readonly IDateTimeProvider _dateTimeProvider;
            private readonly IServiceProvider _serviceProvider;
            private readonly ILoggerFactory _loggerFactory;
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

                var streamLogger = _loggerFactory?.CreateLogger<DisposeAwareStream>();
                _stream = new DisposeAwareStream(_tcpClient.GetStream(), _dateTimeProvider, OnDebugStreamsCloses, streamLogger);
                _rpcHost = new ProxyHost(_stream, serviceProvider);
            }

            private Task OnDebugStreamsCloses()
            {
                Dispose();
                return Task.CompletedTask;
            }

            public void Dispose()
            {
                _stream.Dispose();
                _rpcHost.Dispose();
                _rpcHost.Dispose();
                _debugServer._debugSessions.TryRemove(this, out _);
            }
        }
    }
}
