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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI4E.Utils.Messaging.Primitives
{
    public sealed class MessageFrameBuilderStream : Stream
    {
        private readonly MessageFrameBuilder _frame;
        private ReadOnlyMemory<byte> _readMemory;
        private Memory<byte> _writeMemory;
        private int _position;
        private bool _touched;
        private int _length;

        public MessageFrameBuilderStream(MessageFrameBuilder frame, bool overrideContent)
        {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            _frame = frame;
            _readMemory = _frame.Payload;
            _position = overrideContent ? _readMemory.Length : 0;
            _length = _readMemory.Length;
        }

        public override void Flush()
        {
            if (_touched)
            {
                var readMemory = _writeMemory.Slice(start: 0, _length);
                _frame.UnsafeReplacePayloadWithoutCopy(readMemory);

                _readMemory = readMemory;
                _writeMemory = default;
                _touched = false;
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

            return Read(buffer.AsSpan().Slice(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ReadOnlyMemory<byte> slice;
            var sliceLength = Math.Min(_length - _position, buffer.Length);

            if (!_touched)
            {
                slice = _readMemory.Slice(start: _position, sliceLength);
            }
            else
            {
                slice = _writeMemory.Slice(start: _position, sliceLength);
            }

            var dest = buffer.Slice(0, slice.Length);
            slice.Span.CopyTo(dest);
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

        // Internal for test purposes only.
        internal void Touch(int minLength)
        {
            Debug.Assert(!_touched);

            _writeMemory = new byte[Math.Max(_readMemory.Length, minLength)];
            _readMemory.CopyTo(_writeMemory);
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
                Debug.Assert(writeMemory.Length >= minLength);

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

            Write(buffer.AsSpan().Slice(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // Our current position in the stream does not have to be at the end of the stream.
            // Compute the new stream length, which is either the length it has before or the position, 
            // we are writing at plus the number of bytes, whichever is greater.
            var newLength = Math.Max(_length, _position + buffer.Length);
            EnsureLength(newLength);

            buffer.CopyTo(_writeMemory.Span.Slice(_position));
            _length = newLength;
            Position += buffer.Length;
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
                if (value < 0 || value > _length)
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
