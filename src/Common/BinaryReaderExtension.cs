using System;
using System.IO;
using System.Text;

namespace AI4E.Internal
{
    internal static class BinaryReaderExtension
    {
        private static readonly byte[] _emptyBytes = new byte[0];

        public static byte[] ReadBytes(this BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var length = reader.ReadInt32();

            if (length == 0)
            {
                return _emptyBytes;
            }

            return reader.ReadBytes(length);
        }

        public static ReadOnlyMemory<char> ReadUtf8(this BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var bytesCount = reader.ReadInt32();

            if (bytesCount == 0)
                return ReadOnlyMemory<char>.Empty;

            var bytes = reader.ReadBytes(bytesCount); // TODO: This creates a new array
            var result = Encoding.UTF8.GetString(bytes);

            return result.AsMemory();
        }
    }
}
