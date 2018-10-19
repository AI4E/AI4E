/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ServerEndPoint.cs 
 * Types:           (1) AI4E.Routing.SignalR.Server.ServerEndPoint
 *                  (2) AI4E.Routing.SignalR.Server.ServerEndPoint.ServerCallStub
 * Version:         1.0
 * Author:          Andreas Trütschel
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
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Remoting;
using AI4E.Routing.SignalR.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Server
{
    /// <summary>
    /// Represents the server end-point, directly wrapping an underlying signal-r connection.
    /// </summary>
    /// <remarks>
    /// The type and the corresponding client-side types implement a protocol 
    /// to abstract away the signal-r connection and provide a persistent connection
    /// across multiple connections (automatic reconnection). It does not validate client authentication.
    /// 
    /// This type is registered as singleton in the dependency injection container normally and is not instanciated directly.
    /// This type is not meant to be used directly but is supporting the signal-r messaging stack.
    /// </remarks>
    public sealed class ServerEndPoint : IServerEndPoint
    {
        #region Fields

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServerEndPoint> _logger;

        // Store and forward queues.
        private readonly AsyncProducerConsumerQueue<(IMessage message, string address)> _inboundMessages;
        private readonly OutboundMessageLookup _outboundMessages = new OutboundMessageLookup();

        private int _nextSeqNum = 1;

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="ServerEndPoint"/> type.
        /// </summary>
        /// <param name="serviceProvider">The server provider used to request service instances.</param>
        /// <param name="logger">A logger used for logging or null.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
        public ServerEndPoint(IServiceProvider serviceProvider, ILogger<ServerEndPoint> logger = null)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
            _logger = logger;

            _inboundMessages = new AsyncProducerConsumerQueue<(IMessage message, string address)>();
        }

        #endregion

        #region IServerEndPoint

        /// <summary>
        /// Asynchronously received a message from any connected clients.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the received message and the address of the client the sent the message.</returns>
        public Task<(IMessage message, string address)> ReceiveAsync(CancellationToken cancellation)
        {
            return _inboundMessages.DequeueAsync(cancellation);
        }

        /// <summary>
        /// Asynchronously sends a message to the specified client.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="address">The address of the client.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any of <paramref name="message"/> or <paramref name="address"/> is null</exception>
        public async Task SendAsync(IMessage message, string address, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            var length = checked((int)message.Length);
            var bytes = ArrayPool<byte>.Shared.Rent(checked((int)message.Length));

            try
            {
                var memory = bytes.AsMemory().Slice(start: 0, length);
                using (var stream = new MemoryStream(bytes, writable: true))
                {
                    await message.WriteAsync(stream, cancellation);
                }

                await SendAsync(memory, address, cancellation);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        #endregion

        private async Task ReceiveAsync(int seqNum, ReadOnlyMemory<byte> memory, string address)
        {
            var message = new Message();

            using (var stream = new ReadOnlyStream(memory))
            {
                await message.ReadAsync(stream, cancellation: default);
            }

            await _inboundMessages.EnqueueAsync((message, address));

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();

            await hubContext.Clients.Client(address).AckAsync(seqNum);
        }

        private void Ack(int seqNum, string address)
        {
            var success = _outboundMessages.TryRemove(seqNum, out var compareAddress, out _, out var ackSource) &&
                          ackSource.TrySetResult(null);
            Assert(success);
            Assert(compareAddress == address);
        }

        private async Task InitAsync(string address, string previousAddress)
        {
            if (previousAddress == null)
                return;

            var messages = _outboundMessages.Update(previousAddress, address);

            var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();
            await Task.WhenAll(messages.Select(p => hubContext.Clients.Client(address).DeliverAsync(p.seqNum, Base64Coder.ToBase64String(p.bytes.Span))));
        }

        private async Task SendAsync(ReadOnlyMemory<byte> bytes, string address, CancellationToken cancellation)
        {
            var ackSource = new TaskCompletionSource<object>();
            var seqNum = GetNextSeqNum();

            while (!_outboundMessages.TryAdd(seqNum, address, bytes, ackSource))
            {
                seqNum = GetNextSeqNum();
            }

            try
            {
                // We cannot assume that the operation is truly cancelled. 
                // It is possible that the cancellation is invoked, when the message is just acked,
                // but before the delegate is unregistered from the cancellation token.

                var hubContext = _serviceProvider.GetRequiredService<IHubContext<ServerCallStub, ICallStub>>();
                var base64 = Base64Coder.ToBase64String(bytes.Span);

                var tasks = new[] { hubContext.Clients.Client(address).DeliverAsync(seqNum, base64), ackSource.Task };
                await Task.WhenAll(tasks).WithCancellation(cancellation);
            }
            catch
            {
                if (_outboundMessages.TryRemove(seqNum))
                {
                    ackSource.TrySetCanceled();
                }

                throw;
            }
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        public void Dispose() { }

        internal sealed class ServerCallStub : Hub<ICallStub>, IServerCallStub
        {
            private readonly ServerEndPoint _endPoint;

            public ServerCallStub(ServerEndPoint endPoint)
            {
                if (endPoint == null)
                    throw new ArgumentNullException(nameof(endPoint));

                _endPoint = endPoint;
            }

            public async Task DeliverAsync(int seqNum, string base64) // byte[] bytes)
            {
                var bytesLength = Base64Coder.ComputeBase64DecodedLength(base64.AsSpan());
                var bytes = ArrayPool<byte>.Shared.Rent(bytesLength);

                try
                {
                    var success = Base64Coder.TryFromBase64Chars(base64.AsSpan(), bytes, out var bytesWritten);
                    Assert(success);

                    var memory = bytes.AsMemory().Slice(start: 0, length: bytesWritten);

                    await _endPoint.ReceiveAsync(seqNum, memory, Context.ConnectionId);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(bytes);
                }
            }

            public Task AckAsync(int seqNum)
            {
                _endPoint.Ack(seqNum, Context.ConnectionId);

                return Task.CompletedTask;
            }

            public async Task<string> InitAsync(string previousAddress)
            {
                await _endPoint.InitAsync(Context.ConnectionId, previousAddress);
                return Context.ConnectionId;
            }
        }
    }
}
