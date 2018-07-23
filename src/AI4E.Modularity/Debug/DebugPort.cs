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
using System.IO;
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
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly ConcurrentDictionary<DebugSession, byte> _debugSessions = new ConcurrentDictionary<DebugSession, byte>();

        #endregion

        #region C'tor

        public DebugPort(IServiceProvider serviceProvider,
                         IAddressConversion<IPEndPoint> addressConversion,
                         IOptions<ModularityOptions> optionsAccessor)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            var options = optionsAccessor.Value ?? new ModularityOptions();

            _serviceProvider = serviceProvider;

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
            await _initializationHelper.CancelAsync();

            await _connectionProcess.TerminateAsync();
        }

        #endregion

        private async Task ConnectProcedure(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var client = await _tcpHost.AcceptTcpClientAsync().WithCancellation(cancellation);
                    var stream = client.GetStream();

                    _debugSessions.TryAdd(new DebugSession(this, stream, _serviceProvider), 0);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception)
                {
                    // TODO: Log exception
                }
            }
        }

        private sealed class DebugSession : IDisposable
        {
            private readonly DebugPort _debugPort;
            private readonly Stream _stream;
            private readonly IServiceProvider _serviceProvider;
            private readonly ProxyHost _rpcHost;

            public DebugSession(DebugPort debugPort,
                                Stream stream,
                                IServiceProvider serviceProvider)
            {
                if (debugPort == null)
                    throw new ArgumentNullException(nameof(debugPort));

                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));

                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                _debugPort = debugPort;
                _stream = stream;
                _serviceProvider = serviceProvider;
                _rpcHost = new ProxyHost(stream, serviceProvider);
            }

            public void Dispose()
            {
                _rpcHost.Dispose();
                _debugPort._debugSessions.TryRemove(this, out _);
            }
        }
    }
}
