using System;
using System.Buffers.Binary;
using System.Text;
using AI4E.Memory.Compatibility;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal struct MemoryWriter<T>
    {
        private readonly Memory<T> _memory;
        private int _position;

        public MemoryWriter(Memory<T> memory)
        {
            _memory = memory;
            _position = 0;
        }

        public void Append(ReadOnlySpan<T> span)
        {
            var spanX = _memory.Span;

            Assert(_position + span.Length <= _memory.Length);
            span.CopyTo(spanX.Slice(_position));
            _position += span.Length;
        }

        public void Append(T c)
        {
            var span = _memory.Span;

            Assert(_position + 1 <= _memory.Length);
            span[_position] = c;
            _position += 1;
        }

        public Memory<T> GetMemory()
        {
            return _memory.Slice(0, _position);
        }
    }

    internal ref struct BinarySpanWriter
    {
        private int _offset;
        private readonly bool _useLittleEndian;

        public BinarySpanWriter(Span<byte> span, ByteOrder byteOrder = ByteOrder.BigEndian)
        {
            if (byteOrder < ByteOrder.Native || byteOrder > ByteOrder.LittleEndian)
            {
                throw new ArgumentException("Invalid enum value.", nameof(byteOrder));
            }

            Span = span;
            ByteOrder = byteOrder;
            _offset = 0;

            if (byteOrder == ByteOrder.Native)
            {
                _useLittleEndian = BitConverter.IsLittleEndian;
            }
            else
            {
                _useLittleEndian = (byteOrder == ByteOrder.LittleEndian);
            }
        }

        public Span<byte> Span { get; }
        public ByteOrder ByteOrder { get; }

        public Span<byte> WrittenSpan => Span.Slice(start: 0, length: _offset);
        public int Length => WrittenSpan.Length;

        public bool CanAdvance(int count)
        {
            if (count < 0)
                throw new ArgumentNullException(nameof(count));

            if (count == 0)
                return true;

            return Span.Length - _offset >= count;
        }

        public bool TryAdvance(int count)
        {
            if (count == 0)
                return true;

            if (CanAdvance(count))
            {
                _offset += count;
                return true;
            }

            return false;
        }

        public void WriteBool(bool value)
        {
            WriteByte(unchecked((byte)(value ? 1 : 0)));
        }

        public void WriteByte(byte value)
        {
            Span.Slice(_offset++)[0] = value;
        }

        public void WriteSByte(sbyte value)
        {
            WriteByte(unchecked((byte)value));
        }

        public void Write(byte[] buffer, bool lengthPrefix = false)
        {
            Write(buffer.AsSpan(), lengthPrefix);
        }

        public void Write(byte[] buffer, int index, int count, bool lengthPrefix = false)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (buffer.Length - index < count)
            {
                throw new ArgumentException(); // TODO
            }

            Write(buffer.AsSpan(index, count), lengthPrefix);
        }

        public void Write(ReadOnlySpan<byte> span, bool lengthPrefix = false)
        {
            if (span.IsEmpty)
                return;

            EnsureSpace(span.Length + (lengthPrefix ? 4 : 0));

            if (lengthPrefix)
            {
                WriteInt32(span.Length);
            }

            span.CopyTo(Span.Slice(_offset));

            _offset += span.Length;
        }

        public void WriteUInt16(ushort value)
        {
            if (_useLittleEndian)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(Span.Slice(_offset), value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(Span.Slice(_offset), value);
            }

            _offset += 2;
        }

        public void WriteInt16(short value)
        {
            if (_useLittleEndian)
            {
                BinaryPrimitives.WriteInt16LittleEndian(Span.Slice(_offset), value);
            }
            else
            {
                BinaryPrimitives.WriteInt16BigEndian(Span.Slice(_offset), value);
            }

            _offset += 2;
        }

        public void WriteUInt32(uint value)
        {
            if (_useLittleEndian)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(Span.Slice(_offset), value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(Span.Slice(_offset), value);
            }

            _offset += 4;
        }

        public void WriteInt32(int value)
        {
            if (_useLittleEndian)
            {
                BinaryPrimitives.WriteInt32LittleEndian(Span.Slice(_offset), value);
            }
            else
            {
                BinaryPrimitives.WriteInt32BigEndian(Span.Slice(_offset), value);
            }

            _offset += 4;
        }

        public void WriteUInt64(ulong value)
        {
            if (_useLittleEndian)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(Span.Slice(_offset), value);
            }
            else
            {
                BinaryPrimitives.WriteUInt64BigEndian(Span.Slice(_offset), value);
            }

            _offset += 8;
        }

        public void WriteInt64(long value)
        {
            if (_useLittleEndian)
            {
                BinaryPrimitives.WriteInt64LittleEndian(Span.Slice(_offset), value);
            }
            else
            {
                BinaryPrimitives.WriteInt64BigEndian(Span.Slice(_offset), value);
            }

            _offset += 8;
        }

        public void WriteSingle(float value)
        {
            var int32 = BitConverter.ToInt32(BitConverter.GetBytes(value), 0); // TODO: *(int*)(&value)

            WriteInt32(int32);
        }

        public void WriteDouble(double value)
        {
            WriteInt64(BitConverter.DoubleToInt64Bits(value));
        }

        public void Write(ReadOnlySpan<char> chars, bool lengthPrefix = true)
        {
            var bytesWritten = Encoding.UTF8.GetBytes(chars, Span.Slice(_offset + (lengthPrefix ? 4 : 0)));

            if (lengthPrefix)
            {
                WriteInt32(bytesWritten);
            }

            _offset += bytesWritten;
        }

        private void EnsureSpace(int count)
        {
            if (CanAdvance(count))
            {
                throw new Exception("Not enought space left"); // TODO
            }
        }
    }

    internal ref struct BinarySpanReader
    {
        private int _offset;
        private readonly bool _useLittleEndian;

        public BinarySpanReader(ReadOnlySpan<byte> span, ByteOrder byteOrder = ByteOrder.BigEndian)
        {
            if (byteOrder < ByteOrder.Native || byteOrder > ByteOrder.LittleEndian)
            {
                throw new ArgumentException("Invalid enum value.", nameof(byteOrder));
            }

            Span = span;
            ByteOrder = byteOrder;
            _offset = 0;

            if (byteOrder == ByteOrder.Native)
            {
                _useLittleEndian = BitConverter.IsLittleEndian;
            }
            else
            {
                _useLittleEndian = (byteOrder == ByteOrder.LittleEndian);
            }
        }

        public ReadOnlySpan<byte> Span { get; }
        public ByteOrder ByteOrder { get; }

        public ReadOnlySpan<byte> ReadSpan => Span.Slice(start: 0, length: _offset);
        public int Length => ReadSpan.Length;

        public bool CanAdvance(int count)
        {
            if (count < 0)
                throw new ArgumentNullException(nameof(count));

            if (count == 0)
                return true;

            return Span.Length - _offset >= count;
        }

        public bool TryAdvance(int count)
        {
            if (count == 0)
                return true;

            if (CanAdvance(count))
            {
                _offset += count;
                return true;
            }

            return false;
        }

        public bool ReadBool()
        {
            return ReadByte() != 0;
        }

        public byte ReadByte()
        {
            return Span.Slice(_offset++)[0];
        }

        public sbyte ReadSByte()
        {
            return unchecked((sbyte)ReadByte());
        }

        public ReadOnlySpan<byte> Read()
        {
            var count = ReadInt32();
            return Read(count);
        }

        public ReadOnlySpan<byte> Read(int count)
        {
            EnsureSpace(count);

            var result = Span.Slice(_offset, count);
            _offset += count;

            return result;
        }

        public ushort ReadUInt16()
        {
            ushort result;

            if (_useLittleEndian)
            {
                result = BinaryPrimitives.ReadUInt16LittleEndian(Span.Slice(_offset));
            }
            else
            {
                result = BinaryPrimitives.ReadUInt16BigEndian(Span.Slice(_offset));
            }

            _offset += 2;

            return result;
        }

        public short ReadInt16()
        {
            short result;

            if (_useLittleEndian)
            {
                result = BinaryPrimitives.ReadInt16LittleEndian(Span.Slice(_offset));
            }
            else
            {
                result = BinaryPrimitives.ReadInt16BigEndian(Span.Slice(_offset));
            }

            _offset += 2;
            return result;
        }

        public uint ReadUInt32()
        {
            uint result;

            if (_useLittleEndian)
            {
                result = BinaryPrimitives.ReadUInt32LittleEndian(Span.Slice(_offset));
            }
            else
            {
                result = BinaryPrimitives.ReadUInt32BigEndian(Span.Slice(_offset));
            }

            _offset += 4;
            return result;
        }

        public int ReadInt32()
        {
            int result;
            if (_useLittleEndian)
            {
                result = BinaryPrimitives.ReadInt32LittleEndian(Span.Slice(_offset));
            }
            else
            {
                result = BinaryPrimitives.ReadInt32BigEndian(Span.Slice(_offset));
            }

            _offset += 4;
            return result;
        }

        public ulong ReadUInt64()
        {
            ulong result;
            if (_useLittleEndian)
            {
                result = BinaryPrimitives.ReadUInt64LittleEndian(Span.Slice(_offset));
            }
            else
            {
                result = BinaryPrimitives.ReadUInt64BigEndian(Span.Slice(_offset));
            }

            _offset += 8;
            return result;
        }

        public long ReadInt64()
        {
            long result;

            if (_useLittleEndian)
            {
                result = BinaryPrimitives.ReadInt64LittleEndian(Span.Slice(_offset));
            }
            else
            {
                result = BinaryPrimitives.ReadInt64BigEndian(Span.Slice(_offset));
            }

            _offset += 8;
            return result;
        }

        public float ReadSingle()
        {
            var int32 = ReadInt32();
            return BitConverter.ToSingle(BitConverter.GetBytes(int32), 0); // TODO: *(float*)(&value)
        }

        public double ReadDouble()
        {
            var int64 = ReadInt64();
            return BitConverter.Int64BitsToDouble(int64);
        }

        public string ReadString()
        {
            var count = ReadInt32();
            return Encoding.UTF8.GetString(Span.Slice(_offset, count));
        }

        private void EnsureSpace(int count)
        {
            if (CanAdvance(count))
            {
                throw new Exception("Not enought space left"); // TODO
            }
        }
    }

    internal enum ByteOrder
    {
        Native,
        BigEndian,
        LittleEndian
    }
}
