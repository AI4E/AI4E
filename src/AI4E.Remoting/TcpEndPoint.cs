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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Memory;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Remoting
{
    public sealed partial class TcpEndPoint : IPhysicalEndPoint<IPEndPoint>
    {
        private readonly ILogger<TcpEndPoint> _logger;

        private readonly Dictionary<IPEndPoint, RemoteEndPoint> _remotes;
        private readonly object _remotesMutex = new object();
        private readonly AsyncProducerConsumerQueue<Transmission<IPEndPoint>> _rxQueue;
        private readonly AsyncProcess _listenerProcess;

        private readonly AsyncDisposeHelper _disposeHelper;

        public TcpEndPoint(
            ILocalAddressResolver<IPAddress> addressResolver,
            ILogger<TcpEndPoint> logger = null)
        {
            if (addressResolver == null)
                throw new ArgumentNullException(nameof(addressResolver));

            _logger = logger;

            _remotes = new Dictionary<IPEndPoint, RemoteEndPoint>();
            _rxQueue = new AsyncProducerConsumerQueue<Transmission<IPEndPoint>>();

            var localAddress = addressResolver.GetLocalAddress();

            if (localAddress == null)
            {
                throw new Exception("Cannot evaluate local address."); // TODO: https://github.com/AI4E/AI4E/issues/32
            }

            TcpListener = new TcpListener(new IPEndPoint(localAddress, 0));
            TcpListener.Start();
            LocalAddress = (IPEndPoint)TcpListener.Server.LocalEndPoint;
            Debug.Assert(LocalAddress != null);

            _listenerProcess = new AsyncProcess(ListenerProcess, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync); // TODO: Do we need synchronization enabled?
        }

        // For test purposes only. TODO: Open this for the public?
        internal TcpEndPoint(
            ILocalAddressResolver<IPAddress> addressResolver,
            int port,
            ILogger<TcpEndPoint> logger = null)
        {
            if (addressResolver == null)
                throw new ArgumentNullException(nameof(addressResolver));

            _logger = logger;

            _remotes = new Dictionary<IPEndPoint, RemoteEndPoint>();
            _rxQueue = new AsyncProducerConsumerQueue<Transmission<IPEndPoint>>();

            var localAddress = addressResolver.GetLocalAddress();

            if (localAddress == null)
            {
                throw new Exception("Cannot evaluate local address."); // TODO: https://github.com/AI4E/AI4E/issues/32
            }

            TcpListener = new TcpListener(new IPEndPoint(localAddress, port));
            TcpListener.Start();
            LocalAddress = (IPEndPoint)TcpListener.Server.LocalEndPoint;
            Debug.Assert(LocalAddress != null);

            _listenerProcess = new AsyncProcess(ListenerProcess, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync); // TODO: Do we need synchronization enabled?
        }

        /// <inheritdoc />
        public IPEndPoint LocalAddress { get; }

        internal TcpListener TcpListener { get; }

        /// <inheritdoc />
        public async ValueTask<Transmission<IPEndPoint>> ReceiveAsync(CancellationToken cancellation = default)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);

                return await _rxQueue.DequeueAsync(guard.Cancellation);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        /// <inheritdoc />
        public async ValueTask SendAsync(Transmission<IPEndPoint> transmission, CancellationToken cancellation = default)
        {
            if (transmission.Equals(default)) // TODO: Use ==
                throw new ArgumentDefaultException(nameof(transmission));

            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);

                if (transmission.RemoteAddress.Equals(LocalAddress))
                {
                    await _rxQueue.EnqueueAsync(transmission);
                    return;
                }

                var remoteEndPoint = GetRemoteEndPoint(transmission.RemoteAddress);
                Debug.Assert(remoteEndPoint != null);
                await remoteEndPoint.SendAsync(transmission.Message, guard.Cancellation);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #region AddressConversion

        /// <inheritdoc />
        public string AddressToString(IPEndPoint address)
        {
            return IPEndPointConverter.AddressToString(address);
        }

        /// <inheritdoc />
        public IPEndPoint AddressFromString(string str)
        {
            return IPEndPointConverter.AddressFromString(str);
        }

        #endregion

        #region ConnectionListener

        private async Task ListenerProcess(CancellationToken cancellation)
        {
            try
            {
                _logger?.LogDebug($"Started physical-end-point on local address '{LocalAddress}'.");

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        var client = await TcpListener.AcceptTcpClientAsync().WithCancellation(cancellation);

                        Task.Run(() => OnClientConnectedAsync(client, cancellation)).HandleExceptions(_logger);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (SocketException)
                    {
                        // The socket is down, we cannot do anything here unless cleaning up the complete end-point.
                        // TODO: Log this
                        Dispose();
                        Debug.Assert(cancellation.IsCancellationRequested);
                        return;
                    }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, $"Physical-end-point {LocalAddress}: Failure on waiting for incoming connections.");
                    }
                }
            }
            finally
            {
                // Kill the socket
                TcpListener.Stop();
            }
        }

        private async Task OnClientConnectedAsync(TcpClient client, CancellationToken cancellation)
        {
            var stream = client.GetStream();
            int remotePort;

            using (ArrayPool<byte>.Shared.RentExact(4, out var buffer))
            {
                await stream.ReadExactAsync(buffer, cancellation);
                remotePort = BinaryPrimitives.ReadInt32LittleEndian(buffer.Span);
            }

            var remoteAddress = new IPEndPoint(((IPEndPoint)client.Client.RemoteEndPoint).Address, remotePort);
            _logger?.LogDebug($"Physical-end-point {LocalAddress}: Remote {remoteAddress} connected.");

            using var guard = await _disposeHelper.GuardDisposalAsync(cancellation);
            bool created;
            RemoteEndPoint remoteEndPoint;

            lock (_remotesMutex)
            {
                created = !_remotes.TryGetValue(remoteAddress, out remoteEndPoint);
                if (created)
                {
                    remoteEndPoint = new RemoteEndPoint(this, remoteAddress, stream, _logger);
                    _remotes.Add(remoteAddress, remoteEndPoint);
                }
            }

            if (!created)
            {
                await remoteEndPoint.OnConnectionRequestedAsync(client);
            }

            Debug.Assert(remoteEndPoint != null);
            Debug.Assert(remoteAddress.Equals(remoteEndPoint.RemoteAddress));
        }

        #endregion

        #region Disposal

        /// <inheritdoc/>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        private async Task DisposeInternalAsync()
        {
            try
            {
                // Terminate the connection listener
                _listenerProcess.Terminate();
            }
            finally
            {
                // Dispose all remote end-points
                ImmutableList<RemoteEndPoint> remotes;

                lock (_remotesMutex)
                {
                    remotes = _remotes.Values.ToImmutableList();
                }

                await remotes.Select(p => p.DisposeAsync()).WhenAll();
            }
        }

        #endregion

        // The calles MUST guard this from disposal, otherwise, a RemoteEndPoint object
        // may be allocated and stored in _remoted that does not get disposed.
        private RemoteEndPoint GetRemoteEndPoint(IPEndPoint address)
        {
            RemoteEndPoint remoteEndPoint;
            lock (_remotesMutex)
            {
                if (!_remotes.TryGetValue(address, out remoteEndPoint))
                {
                    remoteEndPoint = new RemoteEndPoint(this, address, _logger);

                    _remotes.Add(address, remoteEndPoint);
                }
            }

            Debug.Assert(remoteEndPoint != null);
            Debug.Assert(address.Equals(remoteEndPoint.RemoteAddress));

            return remoteEndPoint;
        }

        // For test purposes only.
        internal bool TryGetRemoteEndPoint(IPEndPoint address, out RemoteEndPoint remoteEndPoint)
        {
            lock (_remotesMutex)
            {
                return _remotes.TryGetValue(address, out remoteEndPoint);
            }
        }
    }
}
