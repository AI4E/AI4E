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
using System.Diagnostics;

namespace AI4E.Utils.Messaging.Primitives
{
    public readonly struct MessageFrame : IEquatable<MessageFrame>
    {
        public MessageFrame(ReadOnlySpan<byte> memory)
        {
            Payload = memory.CopyToArray();
        }

        internal MessageFrame(ReadOnlyMemory<byte> memory, bool createCopy)
        {
            if (createCopy)
            {
                Payload = memory.CopyToArray();
            }
            else
            {
                Payload = memory;
            }
        }

        public static MessageFrame UnsafeCreateWithoutCopy(ReadOnlyMemory<byte> memory)
        {
            return new MessageFrame(memory, createCopy: false);
        }

        public int Length => Payload.Length + LengthCodeHelper.Get7BitEndodedIntBytesCount(Payload.Length);

        public ReadOnlyMemory<byte> Payload { get; }

        internal static MessageFrame Read(ReadOnlyMemory<byte> memory, bool createCopy)
        {
            var payloadLength = LengthCodeHelper.Read7BitEncodedInt(memory.Span, out var headerLength);

            memory = memory.Slice(headerLength, payloadLength);
            Debug.Assert(memory.Length == payloadLength);

            return new MessageFrame(memory, createCopy);
        }

        public static MessageFrame Read(ReadOnlySpan<byte> memory)
        {
            return Read(memory.CopyToArray(), createCopy: false);
        }

        public static void Write(in MessageFrame frame, Span<byte> memory)
        {
            LengthCodeHelper.Write7BitEncodedInt(memory, frame.Payload.Length, out var headerLength);
            frame.Payload.Span.CopyTo(memory.Slice(headerLength));
        }

        public MessageFrameStream OpenStream()
        {
            return new MessageFrameStream(this);
        }

        public bool Equals(MessageFrame other)
        {
            if (other.Payload.Length != Payload.Length)
                return false;

            if (other.Payload.Length == 0)
                return Payload.Length == 0;

            return other.Payload.Span.SequenceEqual(Payload.Span);
        }

        public override bool Equals(object? obj)
        {
            return obj is MessageFrame frame && Equals(frame);
        }

        public override int GetHashCode()
        {
            if (Payload.IsEmpty)
                return 0;

            return Payload.Span.SequenceHashCode();
        }

        public static bool operator ==(in MessageFrame left, in MessageFrame right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in MessageFrame left, in MessageFrame right)
        {
            return !left.Equals(right);
        }
    }
}
