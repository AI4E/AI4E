using System;
using System.IO;
using System.Text;

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

        public static void WriteUtf8(this BinaryWriter writer, ReadOnlySpan<char> str)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            var bytes = Encoding.UTF8.GetBytes(str.ToArray()); // TODO: This will copy everything into an array
            var length = bytes.Length;

            writer.Write(length);

            if (length > 0)
            {
                writer.Write(bytes);
            }
        }
    }
}
