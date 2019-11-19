/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    public static class AI4EUtilsStreamExtension
    {
        public static async Task ReadExactAsync(
            this Stream stream,
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellation)
        {
            while (count > 0)
            {
#pragma warning disable CA1062
                var readBytes = await stream.ReadAsync(buffer, offset, count, cancellation)
#pragma warning restore CA1062
                    .ConfigureAwait(false);

                if (readBytes == 0)
                    throw new EndOfStreamException();

                count -= readBytes;
                offset += readBytes;

                Debug.Assert(!(count < 0));
            }
        }

        public static void ReadExact(this Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
#pragma warning disable CA1062
                var readBytes = stream.Read(buffer, offset, count);
#pragma warning restore CA1062

                if (readBytes == 0)
                    throw new EndOfStreamException();

                count -= readBytes;
                offset += readBytes;

                Debug.Assert(!(count < 0));
            }
        }

        public static async Task<byte[]> ToArrayAsync(this Stream stream)
        {
            if (stream == Stream.Null)
            {
                return Array.Empty<byte>();
            }

            if (stream is MemoryStream memoryStream)
            {
                return memoryStream.ToArray();
            }

            using (memoryStream = new MemoryStream())
            {
#pragma warning disable CA1062
                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
#pragma warning restore CA1062
                  

                return memoryStream.ToArray();
            }
        }

        public static async ValueTask<MemoryStream> ReadToMemoryAsync(
            this Stream stream,
            CancellationToken cancellation)
        {
            if (stream is MemoryStream result)
            {
                return result;
            }

#pragma warning disable CA1062
            if (stream.CanSeek)
#pragma warning restore CA1062
            {
                if (stream.Length > int.MaxValue)
                    throw new InvalidOperationException("The streams size exceeds the readable limit.");

                result = new MemoryStream(checked((int)stream.Length));
            }
            else
            {
                result = new MemoryStream();
            }

            await stream.CopyToAsync(result, bufferSize: 1024, cancellation).ConfigureAwait(false);
            result.Position = 0;
            return result;
        }
    }
}
