/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        Message.cs 
 * Types:           (1) AI4E.Remoting.Message
 *                  (2) AI4E.Remoting.MessageFrame
 *                  (3) AI4E.Remoting.MessageFrame.MessageFrameStream
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Remoting
{
    [Serializable]
    public sealed class Message : IMessage
    {
        private static long _headerLength = 12;
        private List<MessageFrame> _frames = new List<MessageFrame>();
        private int _currentIndex = -1;

        public Message() { }

        public long Length => _frames.Aggregate(seed: 0L, (e, n) => e + n.PaddedLength) + _headerLength;

        public MessageFrame CurrentFrame => _currentIndex == -1 ? null : _frames[_currentIndex];

        public int FrameCount => _frames.Count;
        public int FrameIndex => _currentIndex;

        public MessageFrame PushFrame()
        {
            if (_currentIndex == _frames.Count - 1)
            {
                _frames.Add(new MessageFrame());
            }

            _currentIndex++;
            return CurrentFrame;
        }

        public MessageFrame PopFrame()
        {
            if (_currentIndex == -1)
            {
                return null;
            }

            var result = CurrentFrame;
            _currentIndex--;
            return result;
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

        public async Task WriteAsync(Stream stream, CancellationToken cancellation)
        {
            // Write the packet header

            var header = new byte[_headerLength];

            using (var memStream = new MemoryStream(header))
            using (var binaryWriter = new BinaryWriter(memStream))
            {
                binaryWriter.Write(Length);   // 8 byte -- Packet length
                binaryWriter.Write(_currentIndex);
            }

            await stream.WriteAsync(header, 0, header.Length, cancellation);

            // Only send a payload and padding if payload is available.

            var orderedFrames = ((IEnumerable<MessageFrame>)_frames).Reverse();

            foreach (var frame in orderedFrames)
            {
                await frame.WriteAsync(stream, cancellation);
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

            var frames = new List<MessageFrame>();
            var index = 0;

            while (index < buffer.Length - 1)
            {
                var frame = new MessageFrame();
                index = frame.Read(buffer, index, buffer.Length - index);

                frames.Add(frame);
            }

            _frames = new List<MessageFrame>(((IEnumerable<MessageFrame>)frames).Reverse());
            _currentIndex = currentIndex;
        }
    }

    [Serializable]
    public sealed class MessageFrame : IMessageFrame
    {
        internal static readonly long _headerLength = 8;

        private byte[] _payload = new byte[0];
        private int _offset, _length;

        internal MessageFrame() { }

        public long Length => _length + _headerLength;/*(_payload?.Length ?? 0) + _headerLength*/

        internal long PaddedLength => Length + (4 * ((Length + 3) / 4) - Length);

        public Stream OpenStream(bool overrideContent = false)
        {
            return new MessageFrameStream(this, overrideContent);
        }

        internal async Task WriteAsync(Stream stream, CancellationToken cancellation)
        {
            // Write the packet header

            var header = new byte[_headerLength];

            using (var memStream = new MemoryStream(header))
            using (var binaryWriter = new BinaryWriter(memStream))
            {
                binaryWriter.Write(Length);   // 8 byte -- Packet length without padding
            }

            await stream.WriteAsync(header, 0, header.Length, cancellation);

            // Only send a payload and padding if payload is available.
            var padding = checked((int)(4 * ((Length + 3) / 4) - Length));

            Assert(padding >= 0 && padding <= 3);
            Assert((Length + padding) % 4 == 0);

            if (_length > 0)
            {
                await stream.WriteAsync(_payload, _offset, _length, cancellation);

                if (padding > 0)
                {
                    // Just send anything to pad the message.
                    await stream.WriteAsync(header, 0, padding, cancellation);
                }
            }
            else
            {
                // Payload is empty and the header length is a multiple of 4 => There must be no padding
                Assert(padding == 0);
            }
        }

        internal int Read(byte[] buffer, int index, int count)
        {
            long packetLength; // Does NOT include padding.
            int padding;

            if (count < _headerLength)
            {
                throw new IOException("Read past the end of the message.");
            }

            using (var memStream = new MemoryStream(buffer, index, checked((int)_headerLength)))
            using (var binaryReader = new BinaryReader(memStream))
            {
                packetLength = binaryReader.ReadInt64();
            }

            padding = checked((int)(4 * ((packetLength + 3) / 4) - packetLength));
            Assert(padding >= 0 && padding <= 3);

            var payloadLength = packetLength - _headerLength;

            if (payloadLength > 0)
            {
                if (count - checked((int)_headerLength) - payloadLength - padding < 0)
                {
                    throw new IOException("Read past the end of the message.");
                }

                _payload = buffer;
                _offset = index + checked((int)_headerLength);
                _length = checked((int)payloadLength);
            }
            else
            {
                // Payload is empty && the header length is a multiple of 4 => There is no padding
                Assert(padding == 0);
            }

            return index + checked((int)PaddedLength);
        }

        private sealed class MessageFrameStream : Stream
        {
            private readonly MessageFrame _frame;
            private MemoryStream _stream;
            private bool _touched = false;
            private long _readPosition = 0;

            public MessageFrameStream(MessageFrame frame, bool overrideContent)
            {
                _frame = frame;

                if (overrideContent)
                {
                    _stream = new MemoryStream();
                    _touched = true;
                }
                else
                {
                    _stream = new MemoryStream(frame._payload, frame._offset, frame._length);
                }
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!_touched)
                {
                    return _stream.Read(buffer, offset, count);
                }

                var position = _stream.Position;
                _stream.Position = _readPosition;

                var result = _stream.Read(buffer, offset, count);

                _readPosition = _stream.Position;
                _stream.Position = position;

                return result;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!_touched)
                {
                    _readPosition = _stream.Position;
                    _stream = new MemoryStream(count + _frame._length);
                    _stream.Write(_frame._payload, _frame._offset, _frame._length);
                    _touched = true;
                }

                _stream.Write(buffer, offset, count);
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            protected override void Dispose(bool disposing)
            {
                if (_touched)
                {
                    _frame._payload = _stream.ToArray();
                    _frame._length = _frame._payload.Length;
                    _frame._offset = 0;
                }
            }
        }
    }
}
