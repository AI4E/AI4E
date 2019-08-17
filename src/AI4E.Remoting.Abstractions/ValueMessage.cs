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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;

namespace AI4E.Remoting
{
    public readonly struct ValueMessage // TODO: Implement IEquatable, Equals, GetHashCode, etc.?
    {
        private readonly ImmutableList<ValueMessageFrame> _frames;

        internal ValueMessage(IEnumerable<ValueMessageFrame> frames)
        {
            Debug.Assert(frames != null);

            _frames = frames as ImmutableList<ValueMessageFrame> ?? frames.ToImmutableList();
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

        public IReadOnlyList<ValueMessageFrame> Frames => _frames ?? ImmutableList<ValueMessageFrame>.Empty;

        public ValueMessage PushFrame(in ValueMessageFrame frame)
        {
            return new ValueMessage((_frames ?? ImmutableList<ValueMessageFrame>.Empty).Add(frame));
        }

        public ValueMessage PopFrame(out ValueMessageFrame frame)
        {
            if (_frames == null || _frames.Count == 0)
            {
                frame = default;
                return this;
            }

            frame = _frames[_frames.Count - 1];
            return new ValueMessage(_frames.RemoveAt(_frames.Count - 1));
        }

        public ValueMessageFrame PeekFrame()
        {
            if (_frames == null || _frames.Count == 0)
            {
                return default;
            }

            return _frames[_frames.Count - 1];
        }

        public static ValueMessage ReadFromMemory(ReadOnlySpan<byte> memory)
        {
            var framesLength = LengthCodeHelper.Read7BitEncodedInt(memory, out var headerLength);
            var buffer = memory.Slice(headerLength, framesLength);
            var framesBuilder = ImmutableList.CreateBuilder<ValueMessageFrame>();

            while (buffer.Length > 0)
            {
                var frame = ValueMessageFrame.Read(buffer);
                buffer = buffer.Slice(frame.Length);
                framesBuilder.Add(frame);
            }

            var frames = framesBuilder.ToImmutable();
            return new ValueMessage(frames);
        }

        public static void WriteToMemory(in ValueMessage message, Span<byte> memory)
        {
            if (memory.Length < message.Length)
                throw new ArgumentException("The messages size is larger than the length of the span.");

            memory = memory.Slice(0, message.Length);

            LengthCodeHelper.Write7BitEncodedInt(memory, message.GetFramesLength(), out var headerLength);

            memory = memory.Slice(headerLength);

            foreach (var frame in message.Frames)
            {
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(frame.Length);

                ValueMessageFrame.Write(frame, memory);
                memory = memory.Slice(frame.Length);
            }
        }

        public static async ValueTask<ValueMessage> ReadFromStreamAsync(Stream stream, CancellationToken cancellation)
        {
            var framesLength = await LengthCodeHelper.Read7BitEncodedIntAsync(stream, cancellation);
            var buffer = new byte[framesLength].AsMemory();
            await stream.ReadExactAsync(buffer, cancellation);

            var framesBuilder = ImmutableList.CreateBuilder<ValueMessageFrame>();

            while (buffer.Length > 0)
            {
                var frame = ValueMessageFrame.Read(buffer, createCopy: false);
                buffer = buffer.Slice(frame.Length);
                framesBuilder.Add(frame);
            }

            var frames = framesBuilder.ToImmutable();
            return new ValueMessage(frames);
        }

        public static async ValueTask WriteToStreamAsync(ValueMessage message, Stream stream, CancellationToken cancellation)
        {
            await LengthCodeHelper.Write7BitEncodedIntAsync(stream, message.GetFramesLength(), cancellation);

            foreach (var frame in message.Frames)
            {
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(frame.Length);
                var buffer = bufferOwner.Memory.Slice(0, frame.Length);

                ValueMessageFrame.Write(frame, buffer.Span);

                await stream.WriteAsync(buffer, cancellation);
            }
        }

        public static ValueMessage FromFrames(params ValueMessageFrame[] frames)
        {
            return FromFrames(frames as IEnumerable<ValueMessageFrame>);
        }

        public static ValueMessage FromFrames(IEnumerable<ValueMessageFrame> frames)
        {
            if (frames is null)
                throw new ArgumentNullException(nameof(frames));

            return new ValueMessage(frames);
        }

        public static ValueMessage FromFrames(params ReadOnlyMemory<byte>[] frames)
        {
            return FromFrames(frames as IEnumerable<ReadOnlyMemory<byte>>);
        }

        public static ValueMessage FromFrames(IEnumerable<ReadOnlyMemory<byte>> frames)
        {
            if (frames is null)
                throw new ArgumentNullException(nameof(frames));

            return new ValueMessage(frames.Select(p => new ValueMessageFrame(p.Span)));
        }
    }
}
