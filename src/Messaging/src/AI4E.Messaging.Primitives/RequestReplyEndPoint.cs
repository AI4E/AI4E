/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Messaging
{
    public sealed class RequestReplyEndPoint<TPacket> : IDisposable, IAsyncDisposable
        where TPacket : IPacket<TPacket>
    {
        #region Fields

        private readonly IEndPoint<TPacket> _endPoint;
        private readonly ILogger<RequestReplyEndPoint<TPacket>>? _logger;

        private readonly AsyncProducerConsumerQueue<(int seqNum, TPacket packet)> _rxQueue;
        private readonly ConcurrentDictionary<int, ValueTaskCompletionSource<MessageSendResult>> _responseTable;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _cancellationTable;

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncDisposeHelper _disposeHelper;

        private int _nextSeqNum = 1;

        #endregion

        #region C'tor

        public RequestReplyEndPoint(IEndPoint<TPacket> endPoint, ILogger<RequestReplyEndPoint<TPacket>>? logger = null)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            _endPoint = endPoint;
            _logger = logger;

            _rxQueue = new AsyncProducerConsumerQueue<(int seqNum, TPacket packet)>();
            _responseTable = new ConcurrentDictionary<int, ValueTaskCompletionSource<MessageSendResult>>();
            _cancellationTable = new ConcurrentDictionary<int, CancellationTokenSource>();

            _receiveProcess = new AsyncProcess(ReceiveProcessAsync, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Default);
        }

        #endregion

        public async ValueTask<MessageSendResult> SendAsync(TPacket packet, CancellationToken cancellation = default)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                cancellation = guard.Cancellation;

                var responseSource = ValueTaskCompletionSource.Create<MessageSendResult>();
                var seqNum = GetNextSeqNum();

                while (!_responseTable.TryAdd(seqNum, responseSource))
                {
                    seqNum = GetNextSeqNum();
                }

                void RequestCancellation()
                {
                    SendInternalAsync(
                        packet.WithMessage(new Message()), 
                        messageType: RequestReplyMessageType.CancellationRequest, 
                        handled: false, 
                        corrId: seqNum, 
                        cancellation: default).HandleExceptions(_logger);
                }

                cancellation.ThrowIfCancellationRequested();

                using (cancellation.Register(RequestCancellation))
                {
                    await SendInternalAsync(
                        packet, 
                        seqNum, 
                        RequestReplyMessageType.Request, 
                        handled: false, 
                        corrId: 0, 
                        cancellation).ConfigureAwait(false);

                    // The tasks gets canceled if cancellation is requested and we receive a cancellation response from 
                    // the client.
                    return await responseSource.Task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async ValueTask<MessageReceiveResult<TPacket>> ReceiveAsync(CancellationToken cancellation = default)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                cancellation = guard.Cancellation;

                var (seqNum, packet) = await _rxQueue.DequeueAsync(cancellation).ConfigureAwait(false);
                var cancellationTokenSource = _cancellationTable.GetOrAdd(seqNum, new CancellationTokenSource());
                return new MessageReceiveResult<TPacket>(this, seqNum, packet, cancellationTokenSource);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #region Receive

        private async Task ReceiveProcessAsync(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var packet = await _endPoint.ReceiveAsync(cancellation).ConfigureAwait(false);

                    await HandleMessageAsync(packet).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
#pragma warning disable CA1031
                catch
#pragma warning restore CA1031
                {
                    // TODO: Log
                }
            }
        }

        private async Task HandleMessageAsync(TPacket packet)
        {
            var message = packet.Message;
            var (seqNum, messageType, handled, corrId) = DecodeMessage(ref message);

            ValueTaskCompletionSource<MessageSendResult> responseSource;
            switch (messageType)
            {
                case RequestReplyMessageType.Request:
                    _cancellationTable.GetOrAdd(seqNum, _ => new CancellationTokenSource());

                    await _rxQueue.EnqueueAsync((seqNum, packet)).ConfigureAwait(false);
                    break;

                case RequestReplyMessageType.Response:

                    // We did not already receive a response for this corr-id.
                    if (_responseTable.TryRemove(corrId, out responseSource))
                    {
                        responseSource.SetResult(new MessageSendResult(message, handled));
                    }
                    break;

                case RequestReplyMessageType.CancellationRequest:
                    if (_cancellationTable.TryGetValue(corrId, out var cancellationSource))
                    {
                        cancellationSource.Cancel();
                    }
                    break;

                case RequestReplyMessageType.CancellationResponse:
                    // We did not already receive a response for this corr-id.
                    if (_responseTable.TryGetValue(corrId, out responseSource))
                    {
                        responseSource.TrySetCanceled();
                    }
                    break;

                default:
                    // await SendBadMessageAsync(address, seqNum);
                    break;
            }
        }

        #endregion

        #region Disposal

        /// <inheritdoc/>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private Task DisposeInternalAsync()
        {
            return _receiveProcess.TerminateAsync();
        }

        #endregion

        #region Send

        private async ValueTask SendInternalAsync(
            TPacket packet, 
            int seqNum, 
            RequestReplyMessageType messageType,
            bool handled, 
            int corrId, 
            CancellationToken cancellation)
        {
            var message = packet.Message;

            EncodeMessage(ref message, seqNum, messageType, handled, corrId);

            await _endPoint.SendAsync(packet.WithMessage(message), cancellation).ConfigureAwait(false);

        }

        private ValueTask SendInternalAsync(
            TPacket packet, 
            RequestReplyMessageType messageType, 
            bool handled, 
            int corrId, 
            CancellationToken cancellation)
        {
            return SendInternalAsync(packet, GetNextSeqNum(), messageType, handled, corrId, cancellation);
        }

        // Send the specified response and end the request.
        internal ValueTask SendResultAsync(
            MessageSendResult result, 
            TPacket packet, 
            int seqNum, 
            CancellationToken cancellation)
        {
            return SendInternalAsync(
                packet.WithMessage(result.Message),
                RequestReplyMessageType.Response,
                result.Handled,
                corrId: seqNum,
                cancellation: cancellation);
        }

        internal ValueTask SendCancellationAsync(TPacket packet, int seqNum)
        {
            // The handled parameter can be of any value here, as it is ignored by the receiver currently.
            return SendInternalAsync(
                packet.WithMessage(new Message()),
                messageType: RequestReplyMessageType.CancellationResponse,
                handled: false,
                corrId: seqNum,
                cancellation: default);
        }

        #endregion

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        // TODO: Use SpanReader/Writer API
        private static (int seqNum, RequestReplyMessageType messageType, bool handled, int corrId) DecodeMessage(
            ref Message message)
        {
            message = message.PopFrame(out var frame);

            using var frameStream = frame.OpenStream();
            using var reader = new BinaryReader(frameStream);

            var seqNum = reader.ReadInt32();
            var messageType = (RequestReplyMessageType)reader.ReadInt32();
            var handled = reader.ReadBoolean();
            reader.ReadInt16();
            reader.ReadByte();
            var corrId = reader.ReadInt32();

            return (seqNum, messageType, handled, corrId);
        }

        private static void EncodeMessage(
            ref Message message, int seqNum, RequestReplyMessageType messageType, bool handled, int corrId)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write(seqNum);           // 4 bytes
                writer.Write((int)messageType); // 4 bytes
                writer.Write(handled);          // 1 byte
                writer.Write((short)0);
                writer.Write((byte)0);          // 3 bytes (padding)
                writer.Write(corrId);           // 4 bytes
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        internal void RemoveCancellationRequestSource(int seqNum, CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTable.Remove(seqNum, cancellationTokenSource);
        }
    }

    internal enum RequestReplyMessageType
    {
        CancellationRequest,
        CancellationResponse,
        Request,
        Response
    }
}
