/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        TcpEndPoint.cs 
 * Types:           (1) AI4E.Remoting.TcpEndPoint
                    (2) AI4E.Remoting.TcpEndPoint.LocalEndPoint
                    (3) AI4E.Remoting.TcpEndPoint.Connection
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Remoting
{
    public sealed class TcpEndPoint : IPhysicalEndPoint<IPEndPoint>
    {
        private readonly IAsyncProcess _connectionProcess;
        private readonly TcpListener _tcpHost;
        private readonly ConcurrentDictionary<IPEndPoint, ImmutableList<Connection>> _physicalConnections;
        private readonly ILogger<TcpEndPoint> _logger;
        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public TcpEndPoint(ILogger<TcpEndPoint> logger)
        {
            _physicalConnections = new ConcurrentDictionary<IPEndPoint, ImmutableList<Connection>>();
            _logger = logger;


            _tcpHost = new TcpListener(new IPEndPoint(IPAddress.Loopback, 0));
            _tcpHost.Start();
            LocalAddress = (IPEndPoint)_tcpHost.Server.LocalEndPoint;
            Assert(LocalAddress != null);

            _connectionProcess = new AsyncProcess(ConnectProcedure);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region Initialization

        public Task Initialization => _initializationHelper.Initialization;

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _connectionProcess.StartAsync(cancellation);

            _logger?.LogDebug($"Started physical-end-point on local address '{LocalAddress}'.");
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

        public IPEndPoint LocalAddress { get; }

        private async Task ConnectProcedure(CancellationToken cancellation)
        {
            _logger?.LogDebug($"Physical-end-point {LocalAddress}: Connection process started.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var client = await _tcpHost.AcceptTcpClientAsync();

                    Task.Run(() => OnConnectedAsync(client)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exc) // TODO: This can end in an infinite loop, f.e. if the socket is down.
                {
                    _logger?.LogWarning(exc, $"Physical-end-point {LocalAddress}: Failure on waiting for incoming connections.");
                }
            }
        }

        private void OnConnectedAsync(TcpClient client)
        {
            var stream = client.GetStream();

            Assert(stream != null);

            var remoteAddress = (IPEndPoint)client.Client.RemoteEndPoint;

            _logger?.LogDebug($"Physical-end-point {LocalAddress}: Remote {remoteAddress} connected.");

            var connection = new Connection(this, remoteAddress, stream);

            _physicalConnections.AddOrUpdate(remoteAddress, ImmutableList<Connection>.Empty, (_, current) => current.Add(connection));
        }

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                return await _rxQueue.DequeueAsync(cancellation);
            }
        }

        public async Task SendAsync(IMessage message, IPEndPoint address, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                var connection = await GetConnectionAsync(address, cancellation);

                Assert(connection != null);

                await connection.SendAsync(message, cancellation);
            }
        }

        private Task<Connection> GetConnectionAsync(IPEndPoint address, CancellationToken cancellation)
        {
            if (_physicalConnections.TryGetValue(address, out var list) && list.Any())
            {
                return Task.FromResult(list.First());
            }

            async Task<Connection> ConnectAsync()
            {
                var client = new TcpClient();
                await client.ConnectAsync(address.Address, address.Port);
                var connection = new Connection(this, address, client.GetStream());

                _physicalConnections.AddOrUpdate(address, ImmutableList<Connection>.Empty.Add(connection), (_, current) => current.Add(connection));

                return connection;
            }

            return ConnectAsync().WithCancellation(cancellation);
        }

        private sealed class Connection : IAsyncDisposable
        {
            private readonly TcpEndPoint _endPoint;
            private readonly Stream _stream;
            private readonly IAsyncProcess _receiveProcess;
            private readonly AsyncInitializationHelper _initializationHelper;
            private readonly AsyncDisposeHelper _disposeHelper;
            private readonly AsyncLock _sendLock = new AsyncLock();

            public Connection(TcpEndPoint endPoint, IPEndPoint address, Stream stream)
            {
                if (endPoint == null)
                    throw new ArgumentNullException(nameof(endPoint));

                if (address == null)
                    throw new ArgumentNullException(nameof(address));

                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));

                _endPoint = endPoint;
                RemoteAddress = address;
                _stream = stream;
                _receiveProcess = new AsyncProcess(ReceiveProcedure);
                _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
                _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            }

            public IPEndPoint LocalAddress => _endPoint.LocalAddress;
            public IPEndPoint RemoteAddress { get; }
            private ILogger Logger => _endPoint._logger;

            #region Initialization

            public Task Initialization => _initializationHelper.Initialization;

            private async Task InitializeInternalAsync(CancellationToken cancellation)
            {
                await _receiveProcess.StartAsync(cancellation);
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

                await _receiveProcess.TerminateAsync();
                _stream.Close();

                while (_endPoint._physicalConnections.TryGetValue(RemoteAddress, out var list))
                {
                    var newList = list.Remove(this);

                    if (newList == list)
                        break;

                    if (_endPoint._physicalConnections.TryUpdate(RemoteAddress, newList, list))
                        break;
                }
            }

            #endregion

            private async Task ReceiveProcedure(CancellationToken cancellation)
            {
                Logger?.LogDebug($"Physical-end-point '{_endPoint.LocalAddress}': Started receive process for remote address '{RemoteAddress}'.");

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        var message = await ReceiveAsync(cancellation);

                        _endPoint._rxQueue.EnqueueAsync(message, cancellation).HandleExceptions();
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception exc)
                    {
                        Logger?.LogWarning(exc, $"Physical-end-point '{_endPoint.LocalAddress}': Failure while receiving message from remote address '{RemoteAddress}'.");
                    }
                }
            }

            public async Task SendAsync(IMessage message, CancellationToken cancellation)
            {
                using (await _disposeHelper.ProhibitDisposalAsync())
                {
                    if (_disposeHelper.IsDisposed)
                        throw new ObjectDisposedException(GetType().FullName);

                    try
                    {
                        using (await _sendLock.LockAsync())
                        {
                            await message.WriteAsync(_stream, cancellation);
                        }
                    }
                    catch (IOException)
                    {
                        Dispose();
                        throw;
                    }
                }
            }

            private async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
            {
                var message = new Message();

                try
                {
                    await message.ReadAsync(_stream, cancellation);
                }
                catch (IOException)
                {
                    Close();
                    throw;
                }

                return message;
            }
        }
    }
}
