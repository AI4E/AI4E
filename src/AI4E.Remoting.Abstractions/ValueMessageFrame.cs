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
using AI4E.Internal;

namespace AI4E.Remoting
{
    public readonly struct ValueMessageFrame // TODO: Implement IEquatable, Equals, GetHashCode, etc.?
    {
        public ValueMessageFrame(ReadOnlySpan<byte> memory)
        {
            Payload = memory.CopyToArray();
        }

        internal ValueMessageFrame(ReadOnlyMemory<byte> memory, bool createCopy)
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

        public static ValueMessageFrame UnsafeCreateWithoutCopy(ReadOnlyMemory<byte> memory)
        {
            return new ValueMessageFrame(memory, createCopy: false);
        }

        public int Length => Payload.Length + LengthCodeHelper.Get7BitEndodedIntBytesCount(Payload.Length);

        public ReadOnlyMemory<byte> Payload { get; }

        internal static ValueMessageFrame Read(ReadOnlyMemory<byte> memory, bool createCopy)
        {
            var payloadLength = LengthCodeHelper.Read7BitEncodedInt(memory.Span, out var headerLength);

            memory = memory.Slice(headerLength, payloadLength);
            Debug.Assert(memory.Length == payloadLength);

            return new ValueMessageFrame(memory, createCopy);
        }

        public static ValueMessageFrame Read(ReadOnlySpan<byte> memory)
        {
            return Read(memory.CopyToArray(), createCopy: false);
        }

        public static void Write(in ValueMessageFrame frame, Span<byte> memory)
        {
            LengthCodeHelper.Write7BitEncodedInt(memory, frame.Payload.Length, out var headerLength);
            frame.Payload.Span.CopyTo(memory.Slice(headerLength));
        }

        public ValueMessageFrameStream OpenStream()
        {
            return new ValueMessageFrameStream(this);
        }
    }
}
