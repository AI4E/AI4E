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

using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    public static class AI4EUtilsMemoryStreamExtensions
    {
        public static async ValueTask ReadExactAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellation)
        {
            while (buffer.Length > 0)
            {
#pragma warning disable CA1062
                var readBytes = await stream.ReadAsync(buffer, cancellation);
#pragma warning restore CA1062

                if (readBytes == 0)
                    throw new EndOfStreamException();

                buffer = buffer.Slice(readBytes);
            }
        }

        public static void ReadExact(this Stream stream, Span<byte> buffer)
        {
            while (buffer.Length > 0)
            {
#pragma warning disable CA1062
                var readBytes = stream.Read(buffer);
#pragma warning restore CA1062

                if (readBytes == 0)
                    throw new EndOfStreamException();

                buffer = buffer.Slice(readBytes);
            }
        }
    }
}
