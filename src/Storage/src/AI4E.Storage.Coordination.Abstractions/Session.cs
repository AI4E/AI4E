using System;
using System.Runtime.InteropServices;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Coordination
{
    public readonly struct Session : IEquatable<Session>
    {
        private readonly ReadOnlyMemory<byte> _bytes;

        public Session(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> physicalAddress)
        {
            if (physicalAddress.IsEmpty)
                throw new ArgumentException("The argument must not be an empty span.", nameof(physicalAddress));

            var bytes = (new byte[4 + prefix.Length + physicalAddress.Length]).AsMemory();
            Write(bytes.Span, prefix.Length);
            prefix.CopyTo(bytes.Span.Slice(start: 4));
            physicalAddress.CopyTo(bytes.Span.Slice(start: 4 + prefix.Length));

            _bytes = bytes;
        }

        private Session(ReadOnlyMemory<byte> bytes)
        {
            Assert(!bytes.IsEmpty);

            _bytes = bytes;
        }

        public ReadOnlyMemory<byte> Prefix => _bytes.Slice(start: 4, length: ReadInt32(_bytes.Span));
        public ReadOnlyMemory<byte> PhysicalAddress => _bytes.Slice(start: 4 + ReadInt32(_bytes.Span));

        public bool Equals(Session other)
        {
            return _bytes.Span.SequenceEqual(other._bytes.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is Session session && Equals(session);
        }

        public override int GetHashCode()
        {
            return _bytes.GetHashCode();
        }

        public override string ToString()
        {
            if (_bytes.IsEmpty)
                return string.Empty;

            var resultLenght = Base64Coder.ComputeBase64EncodedLength(_bytes.Span);
            var result = new string('\0', resultLenght);
            var memory = MemoryMarshal.AsMemory(result.AsMemory());

            var writtenMemory = Base64Coder.ToBase64Chars(_bytes.Span, memory.Span);
            Assert(writtenMemory.Length == resultLenght);

            return result;
        }

        public static bool operator ==(Session left, Session right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Session left, Session right)
        {
            return !left.Equals(right);
        }

        public static Session FromChars(ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty)
                return default;

            var bytes = new byte[Base64Coder.ComputeBase64DecodedLength(chars)];
            var bytesLength = Base64Coder.FromBase64Chars(chars, bytes).Length;

            return new Session(bytes.AsMemory().Slice(start: 0, length: bytesLength));
        }

        public static Session FromString(string str)
        {
            return FromChars(str.AsSpan());
        }

        private static void Write(Span<byte> span, int value)
        {
            span[0] = (byte)value;
            span[1] = (byte)(value >> 8);
            span[2] = (byte)(value >> 16);
            span[3] = (byte)(value >> 24);
        }

        private static int ReadInt32(ReadOnlySpan<byte> span)
        {
            return span[0] | span[1] << 8 | span[2] << 16 | span[3] << 24;
        }
    }
}
