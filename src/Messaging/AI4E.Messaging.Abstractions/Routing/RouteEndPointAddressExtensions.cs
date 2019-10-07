/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

using System;
using System.IO;

namespace AI4E.Messaging.Routing
{
    public static class RouteEndPointAddressExtensions
    {
        public static RouteEndPointAddress ReadEndPointAddress(this BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var localEndPointBytesLenght = reader.ReadInt32();

            if (localEndPointBytesLenght == 0)
            {
                return RouteEndPointAddress.UnknownAddress;
            }

            var stream = reader.BaseStream;

            if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var buffer))
            {
                var memory = buffer.AsMemory();
                var position = checked((int)memoryStream.Position);

                if (position - localEndPointBytesLenght > memoryStream.Length)
                {
                    throw new EndOfStreamException();
                }

                return new RouteEndPointAddress(memory.Slice(position, localEndPointBytesLenght));
            }

            var utf8EncodedValue = new byte[localEndPointBytesLenght];
            stream.ReadExact(utf8EncodedValue, offset: 0, count: localEndPointBytesLenght);

            return new RouteEndPointAddress(utf8EncodedValue);
        }

        public static void Write(this BinaryWriter writer, RouteEndPointAddress endPointAddress)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));

            if (endPointAddress == default)
            {
                writer.Write(0);
            }
            var utf8EncodedValue = endPointAddress.Utf8EncodedValue;

            writer.Write(utf8EncodedValue.Length);
            writer.Flush();

            if (utf8EncodedValue.Length > 0)
            {
                var stream = writer.BaseStream;
                stream.Write(utf8EncodedValue.Span);
            }
        }
    }
}
