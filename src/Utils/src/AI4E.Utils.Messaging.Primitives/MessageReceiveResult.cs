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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Messaging.Primitives
{
    public readonly struct MessageReceiveResult<TPacket> : IDisposable
        where TPacket : IPacket<TPacket>
    {
        private static readonly CancellationToken _canceledCancellationToken = BuildCanceledCancellationToken();

        private static CancellationToken BuildCanceledCancellationToken()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            return cancellationTokenSource.Token;
        }

        private readonly RequestReplyEndPoint<TPacket>? _rqRplyEndPoint;
        private readonly int _seqNum;
        private readonly CancellationTokenSource? _cancellationRequestSource;

        internal MessageReceiveResult(
            RequestReplyEndPoint<TPacket> rqRplyEndPoint,
            int seqNum,
            TPacket packet,
            CancellationTokenSource cancellationTokenSource)
        {
            _rqRplyEndPoint = rqRplyEndPoint;
            _seqNum = seqNum;
            Packet = packet;
            _cancellationRequestSource = cancellationTokenSource;
        }

        public CancellationToken Cancellation => _cancellationRequestSource?.Token ?? _canceledCancellationToken;

        public Message Message => Packet.Message;

        public TPacket Packet { get; }

        // Send the specified response and end the request.
        public ValueTask SendResultAsync(MessageSendResult result)
        {
            return _rqRplyEndPoint?.SendResultAsync(result, Packet, _seqNum, Cancellation) ?? default;
        }

        public ValueTask SendAckAsync()
        {
            return SendResultAsync(MessageSendResult.Ack);
        }

        public ValueTask SendResultAsync(Message message)
        {
            return SendResultAsync(new MessageSendResult(message, handled: true));
        }

        public ValueTask SendCancellationAsync()
        {
            return _rqRplyEndPoint?.SendCancellationAsync(Packet, _seqNum) ?? default;
        }

        public void Dispose()
        {
            if (_cancellationRequestSource is null)
                return;

            _cancellationRequestSource.Dispose();
            _rqRplyEndPoint?.RemoveCancellationRequestSource(_seqNum, _cancellationRequestSource);
        }
    }
}
