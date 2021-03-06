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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Memory;
using AI4E.Utils.Messaging.Primitives;
using Microsoft.Extensions.Logging;

namespace AI4E.Remoting
{
    public sealed partial class TcpEndPoint
    {
        internal sealed class RemoteEndPoint : IAsyncDisposable
        {
            private readonly ILogger _logger;

            private readonly ConcurrentDictionary<int, (Message message, ValueTaskCompletionSource ackSource)> _txQueue;
            private readonly ReconnectionManager _reconnectionManager;
            private int _nextSeqNum;

            private RemoteConnection _connection;
            private readonly object _connectionMutex = new object();

            public RemoteEndPoint(TcpEndPoint localEndPoint, IPEndPoint remoteAddress, ILogger logger)
            {
                LocalEndPoint = localEndPoint;
                RemoteAddress = remoteAddress;

                _logger = logger;
                _txQueue = new ConcurrentDictionary<int, (Message message, ValueTaskCompletionSource ackSource)>();
                _reconnectionManager = new ReconnectionManager(this, logger);

                // Initially connect
                _reconnectionManager.Reconnect();
            }

            public RemoteEndPoint(TcpEndPoint localEndPoint, IPEndPoint remoteAddress, Stream stream, ILogger logger)
            {
                LocalEndPoint = localEndPoint;
                RemoteAddress = remoteAddress;

                _logger = logger;
                _txQueue = new ConcurrentDictionary<int, (Message message, ValueTaskCompletionSource ackSource)>();
                _reconnectionManager = new ReconnectionManager(this, logger);

                // Initially connect
                _connection = new RemoteConnection(this, stream);
                _reconnectionManager.Reconnect();
            }

            internal RemoteConnection Connection
            {
                get
                {
                    lock (_connectionMutex)
                    {
                        return _connection;
                    }
                }
            }

            public TcpEndPoint LocalEndPoint { get; }
            public IPEndPoint RemoteAddress { get; }

            public async ValueTask SendAsync(Message message, CancellationToken cancellation)
            {
                var ackSource = ValueTaskCompletionSource.Create();
                var seqNum = GetNextSeqNum();

                while (!_txQueue.TryAdd(seqNum, (message, ackSource)))
                {
                    seqNum = GetNextSeqNum();
                }

                // Try to send the message. If we are unconnected currently or our connection breaks in the meantime,
                // do not execute or cancel the send respectively. The message is already put to the tx-queue.
                // The reconnection process will (re)send the message after the connection is (re)established.

                try
                {
                    // We cannot assume that the operation is truly cancelled. 
                    // It is possible that the cancellation is invoked, when the message is just acked,
                    // but before the delegate is unregistered from the cancellation token.

                    _logger?.LogDebug($"Sending message ({message.Length} total bytes) with seq-num {seqNum}.");

                    var connectionLostToken = _reconnectionManager.ConnectionLost;

                    //  Cancel the send, if the collection is lost in the meantime, as is done in the ordinary send operation.
                    if (!connectionLostToken.IsConnectionLost)
                    {
                        using var cancellationTokenSource = new TaskCancellationTokenSource(connectionLostToken.AsTask(), cancellation);

                        try
                        {
                            await SendInternalAsync(seqNum, message, cancellationTokenSource.CancellationToken);
                        }
                        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                        {
                            _reconnectionManager.Reconnect();
                            // The connection is broken. The message will be re-sent, when reconnected.
                        }
                    }
                    else
                    {
                        _reconnectionManager.Reconnect();
                    }

                    await ackSource.Task.WithCancellation(cancellation);
                }
                catch (Exception exc)
                {
                    // The operation was either cancellation from outside or the object is disposed or something is wrong.
                    if (_txQueue.TryRemove(seqNum, out _))
                    {
                        ackSource.TrySetExceptionOrCanceled(exc);
                    }

                    throw;
                }
            }

            private async Task SendInternalAsync(int seqNum, Message message, CancellationToken cancellation)
            {
                message = EncodeMessage(message, MessageType.Deliver, seqNum);

                RemoteConnection connection;

                lock (_connectionMutex)
                {
                    connection = _connection;
                }

                Debug.Assert(connection != null);
                try
                {
                    await connection.SendAsync(message, cancellation);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    Reconnect();
                }
            }

            internal async ValueTask ReceiveAsync(Message message, CancellationToken cancellation)
            {
                MessageType messageType;
                int seqNum;

                (message, messageType, seqNum) = DecodeMessage(message);

                switch (messageType)
                {
                    case MessageType.Deliver:
                        await ReceiveMessageAsync(message, seqNum, cancellation);
                        break;

                    case MessageType.Ack:
                        await ReceiveAckAsync(seqNum);
                        break;

                    case MessageType.Unknown:
                    default:
                        // TODO: Log
                        break;
                }
            }

            private async ValueTask ReceiveMessageAsync(Message message, int seqNum, CancellationToken cancellation)
            {
                await LocalEndPoint._rxQueue.EnqueueAsync(new Transmission<IPEndPoint>(message, RemoteAddress), cancellation);

                // Send Ack
                message = new Message();
                message = EncodeMessage(message, MessageType.Ack, seqNum);

                RemoteConnection connection;

                lock (_connectionMutex)
                {
                    connection = _connection;
                }

                Debug.Assert(connection != null);
                await connection.SendAsync(message, cancellation);
            }

            private ValueTask ReceiveAckAsync(int seqNum)
            {
                if (_txQueue.TryGetValue(seqNum, out var entry))
                {
                    entry.ackSource.TrySetResult();
                }

                return default;
            }

            #region Connection/Reconnection

            internal void Reconnect()
            {
                _reconnectionManager.Reconnect();
            }

            public ValueTask OnConnectionRequestedAsync(TcpClient client)
            {
                // Ensure we are not connecting to a foreign remote.
                Debug.Assert(RemoteAddress.Address.Equals(((IPEndPoint)client.Client.RemoteEndPoint).Address));

                lock (_connectionMutex)
                {
                    if (_connection != null && _connection.Status == ConnectionStatus.Connected)
                    {
                        _connection.Dispose();
                    }

                    _connection = new RemoteConnection(this, client.GetStream());
                }

                return default;
            }

            public async ValueTask EstablishConnectionAsync(
                bool isInitialConnection, CancellationToken cancellation)
            {
                lock (_connectionMutex)
                {
                    if (_connection != null && _connection.Status == ConnectionStatus.Connected)
                        return;
                }

                var connection = await SetupConnectionAsync(cancellation);

                lock (_connectionMutex)
                {
                    if (_connection == null || _connection.Status == ConnectionStatus.Unconnected)
                    {
                        // We do not need to dispose the connection as it is already in its final (non-connected) state, or null.
                        _connection = connection;
                    }
                }
            }

            private async ValueTask<RemoteConnection> SetupConnectionAsync(CancellationToken cancellation)
            {
                var tcpClient = new TcpClient(new IPEndPoint(LocalEndPoint.LocalAddress.Address, 0));
                await tcpClient.ConnectAsync(RemoteAddress.Address, RemoteAddress.Port).WithCancellation(cancellation);

                var port = LocalEndPoint.LocalAddress.Port;
                var stream = tcpClient.GetStream();

                using (var memoryOwner = MemoryPool<byte>.Shared.RentExact(4))
                {
                    var buffer = memoryOwner.Memory;
                    BinaryPrimitives.WriteInt32LittleEndian(buffer.Span, port);
                    await stream.WriteAsync(buffer, cancellation);
                }

                return new RemoteConnection(this, stream);
            }

            public ValueTask OnConnectionEstablished(CancellationToken cancellation)
            {
                var tasks = _txQueue.Select(p => SendInternalAsync(p.Key, p.Value.message, cancellation));
                return Task.WhenAll(tasks).AsValueTask();
            }

            public ValueTask OnConnectionEstablishing(CancellationToken cancellation)
            {
                return default;
            }

            #endregion

            #region Coding

            private static Message EncodeMessage(Message message, MessageType messageType, int seqNum)
            {
                var frameBuilder = new MessageFrameBuilder();
                using (var frameStream = frameBuilder.OpenStream())
                using (var writer = new BinaryWriter(frameStream))
                {
                    writer.Write((int)messageType);
                    writer.Write(seqNum);
                }

                return message.PushFrame(frameBuilder.BuildMessageFrame());
            }

            private static (Message message, MessageType messageType, int seqNum) DecodeMessage(Message message)
            {
                message = message.PopFrame(out var frame);

                using var frameStream = frame.OpenStream();
                using var reader = new BinaryReader(frameStream);

                var messageType = (MessageType)reader.ReadInt32();
                var seqNum = reader.ReadInt32();

                return (message, messageType, seqNum);
            }

            #endregion

            #region Disposal

            // TODO: Do we have to synchronize this?
            public async ValueTask DisposeAsync()
            {
                _reconnectionManager.Dispose();

                RemoteConnection connection;

                lock (_connectionMutex)
                {
                    connection = _connection;
                }

                if (connection != null)
                {
                    await connection.DisposeAsync();
                }

                lock (LocalEndPoint._remotesMutex)
                {
                    LocalEndPoint._remotes.Remove(RemoteAddress, this);
                }
            }

            #endregion

            private int GetNextSeqNum()
            {
                return Interlocked.Increment(ref _nextSeqNum);
            }
        }

        private enum MessageType
        {
            Unknown,
            Deliver,
            Ack
        }
    }
}
