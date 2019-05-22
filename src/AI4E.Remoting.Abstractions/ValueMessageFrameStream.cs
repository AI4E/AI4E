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
using System.IO;

namespace AI4E.Remoting
{
    public sealed class ValueMessageFrameStream : Stream
    {
        private readonly ValueMessageFrame _frame;
        private int _position;

        public ValueMessageFrameStream(in ValueMessageFrame frame)
        {
            _frame = frame;
        }

        public override void Flush() { }

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

            return Read(buffer.AsSpan().Slice(offset, count));
        }

        public 
#if SUPPORTS_SPAN_APIS
            override
#endif
            int Read(Span<byte> buffer)
        {
            var sliceLength = Math.Min(_frame.Payload.Length - _position, buffer.Length);
            var slice = _frame.Payload.Slice(start: _position, sliceLength);
            var dest = buffer.Slice(0, slice.Length);
            slice.Span.CopyTo(dest);
            _position += slice.Length;

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
                    position = _frame.Payload.Length;
                    break;

                default:
                    throw new ArgumentException("Invalid enum value.", nameof(origin));
            }

            var newPosition = position + offset;

            if (newPosition < 0)
            {
                newPosition = 0;
            }

            if (newPosition > _frame.Payload.Length)
            {
                newPosition = _frame.Payload.Length;
            }

            _position = unchecked((int)newPosition);

            return newPosition;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _frame.Payload.Length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _frame.Payload.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = unchecked((int)value);
            }
        }
    }
}
