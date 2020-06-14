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

using System.Buffers;
using System.Diagnostics;
using System.Text;
using AI4E.Utils;

namespace System.IO
{
    public static class AI4EUtilsMemoryBinaryReaderExtensions
    {
        public static byte[] ReadBytes(this BinaryReader reader)
        {
#pragma warning disable CA1062
            var length = PrefixCodingHelper.Read7BitEncodedInt(reader);
#pragma warning restore CA1062

            if (length == 0)
            {
                return Array.Empty<byte>();
            }

            var result = reader.ReadBytes(length);

            if (result.Length < length)
            {
                throw new EndOfStreamException();
            }

            return result;
        }

        public static SlicedMemoryOwner<byte> ReadBytes(this BinaryReader reader, MemoryPool<byte> memoryPool)
        {
            if (memoryPool is null)
                throw new ArgumentNullException(nameof(memoryPool));

#pragma warning disable CA1062
            var length = PrefixCodingHelper.Read7BitEncodedInt(reader);
#pragma warning restore CA1062

            if (length == 0)
            {
                return default;
            }

            var result = memoryPool.RentExact(length);

            try
            {
                var bytesRead = reader.Read(result.Memory.Span);

                if (bytesRead < length)
                {
                    throw new EndOfStreamException();
                }

                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        public static string ReadUtf8(this BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var bytesCount = PrefixCodingHelper.Read7BitEncodedInt(reader);

            if (bytesCount == 0)
                return string.Empty;

            using var bytesOwner = MemoryPool<byte>.Shared.RentExact(bytesCount);
            var bytesRead = reader.Read(bytesOwner.Memory.Span);

            if (bytesRead < bytesCount)
            {
                throw new EndOfStreamException();
            }

            return Encoding.UTF8.GetString(bytesOwner.Memory.Span);
        }

        public static SlicedMemoryOwner<char> ReadUtf8(this BinaryReader reader, MemoryPool<char> memoryPool)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var bytesCount = PrefixCodingHelper.Read7BitEncodedInt(reader);

            if (bytesCount == 0)
                return default;

            using var bytesOwner = MemoryPool<byte>.Shared.RentExact(bytesCount);
            var bytesRead = reader.Read(bytesOwner.Memory.Span);

            if (bytesRead < bytesCount)
            {
                throw new EndOfStreamException();
            }

            var charCount = Encoding.UTF8.GetCharCount(bytesOwner.Memory.Span);
            var result = memoryPool.RentExact(charCount);

            try
            {
                var charsRead = Encoding.UTF8.GetChars(bytesOwner.Memory.Span, result.Memory.Span);
                Debug.Assert(charsRead == charCount);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
    }
}
