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

using System.Text;
using System.Buffers;
using AI4E.Utils;
using System.Diagnostics;

namespace System.IO
{
    public static class AI4EUtilsMemoryBinaryWriterExtensions
    {
        public static void WriteBytes(this BinaryWriter writer, ReadOnlySpan<byte> bytes)
        {
#pragma warning disable CA1062
            PrefixCodingHelper.Write7BitEncodedInt(writer, bytes.Length);
#pragma warning restore CA1062 
            writer.Write(bytes);
        }

        public static void WriteUtf8(this BinaryWriter writer, ReadOnlySpan<char> chars)
        {
            var bytesCount = Encoding.UTF8.GetByteCount(chars);

#pragma warning disable CA1062
            PrefixCodingHelper.Write7BitEncodedInt(writer, bytesCount);
#pragma warning restore CA1062

            if (bytesCount > 0)
            {
                using var bytesOwner = MemoryPool<byte>.Shared.RentExact(bytesCount);

                var bytesRead = Encoding.UTF8.GetBytes(chars, bytesOwner.Memory.Span);
                Debug.Assert(bytesRead == bytesCount);
                writer.Write(bytesOwner.Memory.Span);
            }
        }
    }
}
