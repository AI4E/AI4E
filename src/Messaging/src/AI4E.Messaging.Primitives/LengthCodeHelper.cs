/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * .NET Core Libraries (CoreFX)
 *   (https://github.com/dotnet/corefx)
 * The MIT License (MIT)
 * 
 * Copyright (c) .NET Foundation and Contributors
 * 
 * All rights reserved.
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Messaging
{
    internal static class LengthCodeHelper
    {
        public static int Get7BitEndodedIntBytesCount(int value)
        {
            var v = (uint)value;

            if ((v & 0xF0_00_00_00) != 0)
                return 5;

            if ((v & 0x0F_E0_00_00) != 0)
                return 4;

            if ((v & 0x00_1F_C0_00) != 0)
                return 3;

            if ((v & 0x00_00_3F_80) != 0)
                return 2;

            return 1;
        }

        public static void Write7BitEncodedInt(Span<byte> buffer, int value, out int bytesWritten)
        {
            bytesWritten = 0;
            var v = (uint)value;
            while (v >= 0x80)
            {
                buffer[bytesWritten] = (byte)(v | 0x80);
                bytesWritten++;
                v >>= 7;
            }

            buffer[bytesWritten] = (byte)v;
            bytesWritten++;
        }

        public static void Write7BitEncodedInt(Span<byte> buffer, int value)
        {
            Write7BitEncodedInt(buffer, value, out _);
        }

        public static async ValueTask Write7BitEncodedIntAsync(Stream stream, int value, CancellationToken cancellation = default)
        {
            using var bufferOwner = MemoryPool<byte>.Shared.Rent(5);
            var buffer = bufferOwner.Memory.Slice(0, 5);

            Write7BitEncodedInt(buffer.Span, value, out var bytesWritten);

            buffer = buffer.Slice(0, bytesWritten);
            await stream.WriteAsync(buffer, cancellation);
        }

        public static int Read7BitEncodedInt(ReadOnlySpan<byte> buffer, out int bytesRead)
        {
            bytesRead = 0;
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            var count = 0;
            var shift = 0;
            byte b;

            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = buffer[bytesRead];
                bytesRead++;

                count |= (b & 0x7F) << shift;
                shift += 7;
            }
            while ((b & 0x80) != 0);

            return count;
        }

        public static int Read7BitEncodedInt(ReadOnlySpan<byte> buffer)
        {
            return Read7BitEncodedInt(buffer, out _);
        }

        public static async ValueTask<int> Read7BitEncodedIntAsync(Stream stream, CancellationToken cancellation = default)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            var count = 0;
            var shift = 0;
            byte b;

            using (var bufferOwner = MemoryPool<byte>.Shared.Rent(1))
            {
                var buffer = bufferOwner.Memory.Slice(0, 1);
                do
                {
                    // Check for a corrupted stream.  Read a max of 5 bytes.
                    // In a future version, add a DataFormatException.
                    if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    {
                        throw new FormatException();
                    }

                    // ReadExactAsync handles end of stream cases for us.
                    await stream.ReadExactAsync(buffer, cancellation);
                    b = buffer.Span[0];

                    count |= (b & 0x7F) << shift;
                    shift += 7;
                }
                while ((b & 0x80) != 0);
            }
            return count;
        }

        public static int Read7BitEncodedInt(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            var count = 0;
            var shift = 0;
            byte b;

            Span<byte> buffer = stackalloc byte[1];

            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadExact handles end of stream cases for us.
                stream.ReadExact(buffer);
                count |= (buffer[0] & 0x7F) << shift;
                shift += 7;
            }
            while ((buffer[0] & 0x80) != 0);

            return count;
        }
    }
}
