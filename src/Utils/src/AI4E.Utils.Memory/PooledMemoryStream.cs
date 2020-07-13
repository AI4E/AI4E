using System;
using System.Buffers;
using System.IO;

namespace AI4E.Utils.Memory
{
    public sealed class PooledMemoryStream : Stream
    {
        private readonly MemoryPool<byte> _memoryPool;
        private IMemoryOwner<byte>? _memoryOwner;
        private bool _requestedMemoryOwner = false;
        private int _length;
        private int _position;

        private readonly ReadOnlyMemory<byte> _memory;

        public PooledMemoryStream(ReadOnlyMemory<byte> memory, MemoryPool<byte>? memoryPool = null)
        {
            _memory = memory;
            _length = memory.Length;
            _memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
        }

        public PooledMemoryStream(int capacity, MemoryPool<byte>? memoryPool = null)
        {
            if (memoryPool is null)
                throw new ArgumentNullException(nameof(memoryPool));

            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _memoryPool = memoryPool ?? MemoryPool<byte>.Shared;
            _memoryOwner = _memoryPool.Rent(capacity == 0 ? 4096 : capacity);
        }

        public PooledMemoryStream(MemoryPool<byte>? memoryPool = null) : this(4096, memoryPool) { }

        public int Capacity => _memoryOwner?.Memory.Length ?? _memory.Length;

        public SlicedMemoryOwner<byte> MemoryOwner
        {
            get
            {
                if (_memoryOwner != null)
                {
                    _requestedMemoryOwner = true;
                    return _memoryOwner.Slice(start: 0, _length);
                }

                var result = _memoryPool.RentExact(_length);
                try
                {
                    _memory.CopyTo(result.Memory);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => _memoryOwner != null;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value <= _length)
                {
                    _position = unchecked((int)value);
                }
                else
                {
                    _position = _length;
                }
            }
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = origin switch
            {
                SeekOrigin.Begin => 0,
                SeekOrigin.Current => _position,
                SeekOrigin.End => _length,
                _ => throw new ArgumentException("Invalid enum value.", nameof(origin)),
            };

            var newPosition = position + offset;

            if (newPosition < 0)
            {
                newPosition = 0;
            }

            if (newPosition > _length)
            {
                if (_memoryOwner is null)
                {
                    newPosition = _length;
                }
                else
                {
                    SetLength(newPosition);
                }
            }

            _position = unchecked((int)newPosition);

            return newPosition;
        }

        public override void SetLength(long value)
        {
            if (_memoryOwner is null)
            {
                throw new NotSupportedException("The stream does not support writing.");
            }

            if (value > int.MaxValue)
                value = int.MaxValue;

            var newLength = unchecked((int)value);

            if (newLength < _length)
            {
                _length = newLength;
            }
            else if (newLength > _length)
            {
                EnsureCapacity(newLength);
                _length = newLength;
            }
        }

        private void EnsureCapacity(int capacity)
        {
            if (capacity <= Capacity)
                return;

            var newCapacity = Capacity == 0 ? 4096 : Capacity;

            while (newCapacity < capacity)
            {
                newCapacity *= 2;
            }

            var oldMemoryOwner = _memoryOwner!;
            var newMemoryOwner = _memoryPool.Rent(newCapacity);

            try
            {
                oldMemoryOwner.Memory.Slice(start: 0, _length).CopyTo(newMemoryOwner.Memory);
            }
            catch
            {
                newMemoryOwner.Dispose();
                throw;
            }

            _memoryOwner = newMemoryOwner;
            _requestedMemoryOwner = false;
            oldMemoryOwner.Dispose();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count > buffer.Length - offset)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");

            return Read(buffer.AsSpan(offset, count));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count > buffer.Length - offset)
                throw new ArgumentException("The sum of offset and count is larger than the buffer length.");

            Write(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            var bytesToRead = Math.Min(_length - _position, buffer.Length);

            if (bytesToRead == 0)
            {
                return 0;
            }

            var memory = _memoryOwner?.Memory ?? _memory;
            memory.Slice(_position, bytesToRead).Span.CopyTo(buffer.Slice(start: 0, bytesToRead));
            _position += bytesToRead;

            return bytesToRead;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_memoryOwner is null)
            {
                throw new NotSupportedException("The stream does not support writing.");
            }

            EnsureCapacity(_position + buffer.Length);

            var memory = _memoryOwner.Memory;
            buffer.CopyTo(memory.Slice(_position, buffer.Length).Span);
            _position += buffer.Length;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !_requestedMemoryOwner)
            {
                _memoryOwner?.Dispose();
            }
        }
    }
}
