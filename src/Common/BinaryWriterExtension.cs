using System;
using System.IO;

namespace AI4E.Internal
{
    internal static class BinaryWriterExtension
    {
        public static void WriteWithLengthPrefix(this BinaryWriter writer, byte[] bytes)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            writer.Write(bytes.Length);

            if (bytes.Length > 0)
            {
                writer.Write(bytes);
            }
        }
    }
}
