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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using static System.Diagnostics.Debug;

// TODO: Remove frame reverse on send and receive

namespace AI4E.Remoting
{
    [Obsolete("Use ValueMessage")]
    [Serializable]
    public sealed class Message : IMessage
    {
        private static readonly int _headerLength = Unsafe.SizeOf<long>() + Unsafe.SizeOf<int>(); // 8 + 4 = 12
        private List<MessageFrame> _frames = new List<MessageFrame>();

        public Message() { }

        public long Length => _frames.Aggregate(seed: 0L, (e, n) => e + n.PaddedLength) + _headerLength;

        public MessageFrame CurrentFrame => FrameIndex == -1 ? null : _frames[FrameIndex];

        public int FrameCount => _frames.Count;
        public int FrameIndex { get; private set; } = -1;

        public MessageFrame PushFrame()
        {
            if (FrameIndex == FrameCount - 1)
            {
                _frames.Add(new MessageFrame());
            }

            FrameIndex++;
            return CurrentFrame;
        }

        public MessageFrame PopFrame()
        {
            if (FrameIndex == -1)
            {
                return null;
            }

            var result = CurrentFrame;
            FrameIndex--;
            return result;
        }

        public void Trim()
        {
            if (FrameIndex == FrameCount - 1)
            {
                return;
            }

            _frames.RemoveRange(FrameIndex + 1, FrameCount - 1);
        }

        IMessageFrame IMessage.CurrentFrame => CurrentFrame;

        IMessageFrame IMessage.PopFrame()
        {
            return PopFrame();
        }

        IMessageFrame IMessage.PushFrame()
        {
            return PushFrame();
        }

        public Span<byte> Write(Span<byte> memory)
        {
            if (memory.Length < Length)
            {
                throw new ArgumentException(); // TODO: Exception message
            }

            var originalMemory = memory;

            // Write header
            var length = Length; // TODO: HTON
            var frameIndex = FrameIndex; // TODO: HTON
            MemoryMarshal.Write(memory, ref length);
            MemoryMarshal.Write(memory.Slice(Unsafe.SizeOf<long>()), ref frameIndex);
            memory = memory.Slice(_headerLength);

            // Write frames
            var orderedFrames = ((IEnumerable<MessageFrame>)_frames).Reverse(); // TODO

            foreach (var frame in orderedFrames)
            {
                memory = frame.Write(memory);
            }

            Assert(memory.Length == originalMemory.Length - Length);

            return originalMemory.Slice(start: 0, checked((int)Length));
        }

        public void Read(ReadOnlySpan<byte> memory)
        {
            if (memory.Length < _headerLength)
            {
                throw new IOException(); // TODO: Exception type and message
            }

            // Read the header 

            var length = MemoryMarshal.Read<long>(memory); // TODO: NTOH
            var frameIndex = MemoryMarshal.Read<int>(memory.Slice(Unsafe.SizeOf<long>())); // TODO: NTOH
            memory = memory.Slice(start: _headerLength, length: checked((int)length - _headerLength));

            var frames = new List<MessageFrame>();

            while (memory.Length > 0)
            {
                var frame = new MessageFrame();
                memory = frame.Read(memory);
                frames.Add(frame);
            }

            _frames = new List<MessageFrame>(((IEnumerable<MessageFrame>)frames).Reverse());
            FrameIndex = frameIndex;
        }

        public async Task WriteAsync(Stream stream, CancellationToken cancellation)
        {
            // Write the packet header

            var header = new byte[_headerLength];

            using (var memStream = new MemoryStream(header))
            using (var binaryWriter = new BinaryWriter(memStream))
            {
                binaryWriter.Write(Length);   // 8 byte -- Packet length
                binaryWriter.Write(FrameIndex);
            }

            await stream.WriteAsync(header, 0, header.Length, cancellation);

            // Only send a payload and padding if payload is available.

            var orderedFrames = ((IEnumerable<MessageFrame>)_frames).Reverse();

            foreach (var frame in orderedFrames)
            {
                // TODO
                var frameBuffer = new byte[frame.PaddedLength];

                frame.Write(frameBuffer);

                await stream.WriteAsync(frameBuffer, offset: 0, count: frameBuffer.Length, cancellation);
            }
        }

        public async Task ReadAsync(Stream stream, CancellationToken cancellation)
        {
            var header = new byte[_headerLength];
            long packetLength;
            int currentIndex;

            await stream.ReadExactAsync(header, 0, header.Length, cancellation);

            using (var memStream = new MemoryStream(header))
            using (var binaryReader = new BinaryReader(memStream))
            {
                packetLength = binaryReader.ReadInt64();
                currentIndex = binaryReader.ReadInt32();
            }

            var buffer = new byte[packetLength - header.LongLength];
            await stream.ReadExactAsync(buffer, 0, buffer.Length, cancellation);

            _frames = ReadFrames(buffer);
            FrameIndex = currentIndex;
        }

        private static List<MessageFrame> ReadFrames(byte[] buffer)
        {
            var frames = new List<MessageFrame>();

            ReadOnlySpan<byte> memory = buffer.AsSpan();

            while (memory.Length > 0)
            {
                var frame = new MessageFrame();
                memory = frame.Read(memory);
                frames.Add(frame);
            }

            return new List<MessageFrame>(((IEnumerable<MessageFrame>)frames).Reverse());
        }

        public byte[] ToArray()
        {
            var buffer = new byte[Length];

            using (var stream = new MemoryStream(buffer, writable: true))
            {
                WriteAsync(stream, cancellation: default).ConfigureAwait(false)
                                                         .GetAwaiter()
                                                         .GetResult();
            }

            return buffer;
        }
    }

    [Obsolete("Use ValueMessageFrame")]
    [Serializable]
    public sealed class MessageFrame : IMessageFrame
    {
        internal static readonly int _headerLength = Unsafe.SizeOf<int>(); // 4 bytes

        private ReadOnlyMemory<byte> _payload = Array.Empty<byte>();

        internal MessageFrame() { }

        public int Length => checked(_payload.Length + _headerLength);
        internal int PaddedLength => checked(Length + (4 * ((Length + 3) / 4) - Length));

        long IMessageFrame.Length => Length;

        public Stream OpenStream(bool overrideContent = false)
        {
            return new MessageFrameStream(this, overrideContent);
        }

        internal Span<byte> Write(Span<byte> memory)
        {
            if (memory.Length - PaddedLength < 0)
            {
                throw new IOException();
            }

            // Write the header
            var packetLength = IPAddress.HostToNetworkOrder(Length);

            MemoryMarshal.Write(memory, ref packetLength);
            memory = memory.Slice(start: _headerLength);

            // Write the payload
            var padding = checked((int)(4 * ((Length + 3) / 4) - Length));

            Assert(padding >= 0 && padding <= 3);
            Assert((Length + padding) % 4 == 0);

            // Only send a payload and padding if payload is available.
            if (!_payload.IsEmpty)
            {
                _payload.Span.CopyTo(memory);
                return memory.Slice(start: _payload.Length + padding);
            }

            // Payload is empty and the header length is a multiple of 4 => There must be no padding
            Assert(padding == 0);
            return memory;
        }

        internal ReadOnlySpan<byte> Read(ReadOnlySpan<byte> memory)
        {
            if (memory.Length - _headerLength < 0)
            {
                throw new IOException("Read past the end of the message.");
            }

            // Read the header 

            // Does NOT include padding but does include the header length.
            var packetLength = IPAddress.NetworkToHostOrder(MemoryMarshal.Read<int>(memory));
            memory = memory.Slice(start: _headerLength);

            // Read the payload
            var padding = 4 * ((packetLength + 3) / 4) - packetLength;
            Assert(padding >= 0 && padding <= 3);
            var payloadLength = packetLength - _headerLength;

            if (payloadLength > 0)
            {
                if (memory.Length - payloadLength - padding < 0)
                {
                    throw new IOException("Read past the end of the message.");
                }

                var payload = new byte[payloadLength];
                memory.Slice(start: 0, payloadLength).CopyTo(payload);
                _payload = payload;
            }
            else
            {
                // Payload is empty && the header length is a multiple of 4 => There is no padding
                Assert(padding == 0);
            }

            // The header is already 'sliced away'
            return memory.Slice(start: payloadLength + padding);
        }

        private sealed class MessageFrameStream : Stream
        {
            private readonly MessageFrame _frame;
            private readonly ReadOnlyMemory<byte> _readMemory;
            private Memory<byte> _writeMemory;
            private int _position;
            private bool _touched;
            private int _length;

            public MessageFrameStream(MessageFrame frame, bool overrideContent)
            {
                _frame = frame;
                _readMemory = _frame._payload;
                _position = overrideContent ? _readMemory.Length : 0;
                _length = _readMemory.Length;
            }

            public override void Flush()
            {
                if (_touched)
                {
                    _frame._payload = _writeMemory.Slice(start: 0, _length);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                if (buffer.Length - offset < count)
                {
                    throw new ArgumentException("The sum of offset and count is larger than the buffer length.");
                }

                ReadOnlyMemory<byte> slice;
                var sliceLength = Math.Min(_length - _position, count);

                if (!_touched)
                {
                    slice = _readMemory.Slice(start: _position, sliceLength);
                }
                else
                {
                    slice = _writeMemory.Slice(start: _position, sliceLength);
                }

                var dest = buffer.AsMemory(offset, slice.Length);
                slice.CopyTo(dest);
                Position += slice.Length;

                return slice.Length;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                int position;

                switch (origin)
                {
                    case SeekOrigin.Begin:
                        position = 0;
                        break;

                    case SeekOrigin.Current:
                        position = _position;
                        break;

                    case SeekOrigin.End:
                        position = _length;
                        break;

                    default:
                        throw new ArgumentException("Invalid enum value.", nameof(origin));
                }

                var newPosition = position + offset;

                if (newPosition < 0)
                {
                    newPosition = 0;
                }

                if (newPosition > int.MaxValue)
                {
                    newPosition = int.MaxValue;
                }

                if (newPosition > _length)
                {
                    SetLength(newPosition);
                }

                _position = unchecked((int)newPosition);

                return newPosition;
            }

            public override void SetLength(long value)
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                int newLength;

                if (value < int.MaxValue)
                {
                    newLength = unchecked((int)value);
                }
                else
                {
                    newLength = int.MaxValue;
                }

                if (_length == newLength)
                {
                    return;
                }

                if (!_touched)
                {
                    Touch(newLength);
                }

                if (newLength > _length)
                {
                    EnsureLength(newLength);

                    // Zero out additional memory.
                    var newSpace = _writeMemory.Slice(_length, newLength - _length);
                    var newSpaceAsIntPtr = MemoryMarshal.Cast<byte, IntPtr>(newSpace.Span);

                    for (var i = 0; i < newSpaceAsIntPtr.Length; i++)
                    {
                        newSpaceAsIntPtr[i] = IntPtr.Zero;
                    }

                    newSpace = newSpace.Slice(start: newSpaceAsIntPtr.Length * Unsafe.SizeOf<IntPtr>());

                    for (var i = 0; i < newSpace.Length; i++)
                    {
                        newSpace.Span[i] = 0;
                    }
                }

                _length = newLength;
            }

            private void Touch(int minLength)
            {
                Assert(!_touched);

                _writeMemory = new byte[Math.Max(_readMemory.Length, minLength)];
                _length = _readMemory.Length;
                _touched = true;
            }

            private void EnsureLength(int minLength)
            {
                if (!_touched)
                {
                    Touch(minLength);
                    return;
                }

                if (_writeMemory.Length < minLength)
                {
                    var writeMemory = new byte[Math.Max(_writeMemory.Length << 1, minLength)];
                    Assert(writeMemory.Length >= minLength);

                    _writeMemory.Slice(start: 0, _length).CopyTo(writeMemory);
                    _writeMemory = writeMemory;
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }

                if (buffer.Length - offset < count)
                {
                    throw new ArgumentException("The sum of offset and count is larger than the buffer length.");
                }

                if (count == 0)
                {
                    return;
                }

                // Our current position in the stream does not have to be at the end of the stream.
                // Compute the new stream length, which is either the length it has before or the position, we are writing at plus the number of bytes, whichever is greater.
                var newLength = Math.Max(_length, _position + count);
                EnsureLength(newLength);

                buffer.AsMemory(offset, count).CopyTo(_writeMemory.Slice(start: _position));
                _length = newLength;
                Position += count;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => true;

            public override long Length => _length;

            public override long Position
            {
                get => _position;
                set
                {
                    if (_position < 0 || _position > _length)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    _position = unchecked((int)value);
                }
            }

            protected override void Dispose(bool disposing)
            {
                Flush();
            }
        }
    }
}
