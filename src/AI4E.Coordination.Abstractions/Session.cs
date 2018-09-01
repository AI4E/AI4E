using System;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public readonly struct Session : IEquatable<Session>
    {
        private readonly ReadOnlyMemory<byte> _bytes;

        public Session(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> physicalAddress)
        {
            if (physicalAddress.IsEmpty)
                throw new ArgumentException("The argument must not be an empty span.", nameof(physicalAddress));

            var bytes = new byte[4 + prefix.Length + physicalAddress.Length];
            Write(bytes, prefix.Length);
            prefix.CopyTo(bytes);
            physicalAddress.CopyTo(bytes);

            _bytes = bytes;
        }

        private Session(ReadOnlyMemory<byte> bytes)
        {
            Assert(!bytes.IsEmpty);

            _bytes = bytes;
        }

        public ReadOnlyMemory<byte> Prefix => _bytes.Slice(start: 0, length: ReadInt32(_bytes.Span));
        public ReadOnlyMemory<byte> PhysicalAddress => _bytes.Slice(start: ReadInt32(_bytes.Span));

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

        public static bool operator ==(Session left, Session right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Session left, Session right)
        {
            return !left.Equals(right);
        }

        public string ToCompactString()
        {
            if (_bytes.IsEmpty)
                return string.Empty;

            // TODO: This will copy everything to a new aray
            return Convert.ToBase64String(_bytes.ToArray());
        }

        public static Session FromCompactString(ReadOnlySpan<char> str)
        {
            if (str.IsEmpty)
                return default;

            // TODO: This will copy everything to a new aray
            var bytes = Convert.FromBase64CharArray(str.ToArray(), 0, str.Length);

            return new Session(bytes);
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
