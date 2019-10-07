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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Messaging.Primitives
{
    public readonly struct Message : IEquatable<Message>
    {
        private readonly ImmutableList<MessageFrame> _frames;

        public Message(IEnumerable<MessageFrame> frames)
        {
            if (frames is null)
                throw new ArgumentNullException(nameof(frames));

            _frames = frames as ImmutableList<MessageFrame> ?? frames.ToImmutableList();
        }

        public int Length
        {
            get
            {
                var framesLength = GetFramesLength();
                return framesLength + LengthCodeHelper.Get7BitEndodedIntBytesCount(framesLength);
            }
        }

        private int GetFramesLength()
        {
            return Frames.Sum(p => p.Length);
        }

        public IReadOnlyList<MessageFrame> Frames => _frames ?? ImmutableList<MessageFrame>.Empty;

        public Message PushFrame(in MessageFrame frame)
        {
            return new Message((_frames ?? ImmutableList<MessageFrame>.Empty).Add(frame));
        }

        public Message PopFrame(out MessageFrame frame)
        {
            if (_frames == null || _frames.Count == 0)
            {
                frame = default;
                return this;
            }

            frame = _frames[_frames.Count - 1];
            return new Message(_frames.RemoveAt(_frames.Count - 1));
        }

        public static Message ReadFromMemory(in ReadOnlySpan<byte> memory)
        {
            var framesLength = LengthCodeHelper.Read7BitEncodedInt(memory, out var headerLength);
            var buffer = memory.Slice(headerLength, framesLength);
            var framesBuilder = ImmutableList.CreateBuilder<MessageFrame>();

            while (buffer.Length > 0)
            {
                var frame = MessageFrame.Read(buffer);
                buffer = buffer.Slice(frame.Length);
                framesBuilder.Add(frame);
            }

            var frames = framesBuilder.ToImmutable();
            return new Message(frames);
        }

        public static void WriteToMemory(in Message message, Span<byte> memory)
        {
            if (memory.Length < message.Length)
                throw new ArgumentException("The messages size is larger than the length of the span.");

            memory = memory.Slice(0, message.Length);

            LengthCodeHelper.Write7BitEncodedInt(memory, message.GetFramesLength(), out var headerLength);

            memory = memory.Slice(headerLength);

            foreach (var frame in message.Frames)
            {
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(frame.Length);

                MessageFrame.Write(frame, memory);
                memory = memory.Slice(frame.Length);
            }
        }

        public static async ValueTask<Message> ReadFromStreamAsync(Stream stream, CancellationToken cancellation)
        {
            var framesLength = await LengthCodeHelper.Read7BitEncodedIntAsync(stream, cancellation);
            var buffer = new byte[framesLength].AsMemory();
            await stream.ReadExactAsync(buffer, cancellation);

            var framesBuilder = ImmutableList.CreateBuilder<MessageFrame>();

            while (buffer.Length > 0)
            {
                var frame = MessageFrame.Read(buffer, createCopy: false);
                buffer = buffer.Slice(frame.Length);
                framesBuilder.Add(frame);
            }

            var frames = framesBuilder.ToImmutable();
            return new Message(frames);
        }

        public static async ValueTask WriteToStreamAsync(Message message, Stream stream, CancellationToken cancellation)
        {
            await LengthCodeHelper.Write7BitEncodedIntAsync(stream, message.GetFramesLength(), cancellation);

            foreach (var frame in message.Frames)
            {
                using var bufferOwner = MemoryPool<byte>.Shared.RentExact(frame.Length);
                var buffer = bufferOwner.Memory;

                MessageFrame.Write(frame, buffer.Span);

                await stream.WriteAsync(buffer, cancellation);
            }
        }

        public static void WriteToStream(Message message, Stream stream)
        {
            Span<byte> memory = stackalloc byte[5];

            LengthCodeHelper.Write7BitEncodedInt(memory, message.GetFramesLength(), out var memoryLength);
            stream.Write(memory.Slice(start: 0, memoryLength));

            foreach (var frame in message.Frames)
            {
                using var bufferOwner = MemoryPool<byte>.Shared.RentExact(frame.Length);
                var buffer = bufferOwner.Memory;
                MessageFrame.Write(frame, buffer.Span);
                stream.Write(buffer.Span);
            }
        }

        public bool Equals(Message other)
        {
            if (other.Frames.Count != Frames.Count)
                return false;

            if (other.Frames.Count == 0)
                return Frames.Count == 0;

            return other.Frames.SequenceEqual(Frames);
        }

        public override bool Equals(object? obj)
        {
            return obj is Message message && Equals(message);
        }

        public override int GetHashCode()
        {
            if (Frames.Count == 0)
                return 0;

            return Frames.GetSequenceHashCode();
        }

        public static bool operator ==(in Message left, in Message right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in Message left, in Message right)
        {
            return !left.Equals(right);
        }
    }
}
