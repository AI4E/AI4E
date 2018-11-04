using System;
using System.Buffers;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class MemoryExtensions
    {
        public static ArrayPoolExtension.ArrayPoolReleaser<byte> Base64Decode(this string str, out Memory<byte> bytes)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return Base64Decode(str.AsSpan(), out bytes);
        }

        public static ArrayPoolExtension.ArrayPoolReleaser<byte> Base64Decode(in this ReadOnlySpan<char> chars, out Memory<byte> bytes)
        {
            var minBytesLength = Base64Coder.ComputeBase64DecodedLength(chars);
            var releaser = ArrayPool<byte>.Shared.RentExact(minBytesLength, out bytes);

            try
            {
                var success = Base64Coder.TryFromBase64Chars(chars, bytes.Span, out var bytesWritten);
                Assert(success);

                bytes = bytes.Slice(start: 0, length: bytesWritten);

                return releaser;
            }
            catch
            {
                releaser.Dispose();
                throw;
            }
        }
    }
}
