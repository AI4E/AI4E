using System;
using System.IO;

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
    }
}
