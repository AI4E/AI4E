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
using System.IO;

namespace AI4E.Utils
{
    public sealed class ReadOnlyStream : Stream
    {
        private readonly ReadOnlyMemory<byte> _memory;
        private int _position;

        public ReadOnlyStream(ReadOnlyMemory<byte> memory)
        {
            _memory = memory;
            _position = 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _memory.Length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _memory.Length - 1)
                    throw new ArgumentOutOfRangeException(nameof(value));

                Debug.Assert(value <= int.MaxValue);

                _position = unchecked((int)value);
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (buffer.Length - offset < count)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");

            return Read(buffer.AsSpan(offset, count));
        }

#if SUPPORTS_SPAN_APIS
        public override int Read(Span<byte> buffer)
#else
        public int Read(Span<byte> buffer)
#endif
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            if (_position == _memory.Length)
            {
                return 0;
            }

            var readableBytes = Math.Min(_memory.Length - _position, buffer.Length);
            var span = _memory.Span;

            span.Slice(_position, readableBytes).CopyTo(buffer);

            _position += readableBytes;

            Debug.Assert(_position <= _memory.Length);

            return readableBytes;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin < SeekOrigin.Begin || origin > SeekOrigin.End)
                throw new ArgumentOutOfRangeException(nameof(origin));

            long position;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;

                case SeekOrigin.Current:
                    position = _position + offset;
                    break;

                case SeekOrigin.End:
                    position = _position + offset;
                    break;

                default:
                    return _position;
            }

            if (position < 0)
                position = 0;

            if (position > _memory.Length - 1)
                position = _memory.Length - 1;

            Debug.Assert(position <= int.MaxValue);

            return _position = unchecked((int)position);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
