using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Memory.Compatibility;

namespace AI4E.Internal
{
    internal static partial class StreamExtension
    {
        public static async Task ReadExactAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellation)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            while (buffer.Length > 0)
            {
                var readBytes = await stream.ReadAsync(buffer, cancellation);

                if (readBytes == 0)
                    throw new EndOfStreamException();

                buffer = buffer.Slice(readBytes);

                Debug.Assert(!(buffer.Length < 0));
            }
        }

        public static void ReadExact(this Stream stream, Span<byte> buffer)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            while (buffer.Length > 0)
            {
                var readBytes = stream.Read(buffer);

                if (readBytes == 0)
                    throw new EndOfStreamException();

                buffer = buffer.Slice(readBytes);

                Debug.Assert(!(buffer.Length < 0));
            }
        }
    }
}
