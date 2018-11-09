/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EndPointAddress.cs 
 * Types:           (1) AI4E.Routing.EndPointAddress
 *                  (2) AI4E.Routing.EndPointAddressExtension
 *                  (3) AI4E.Routing.EndPointAddressJsonConverter
 * Version:         1.0
 * Author:          Andreas Trütschel
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using AI4E.Internal;
using AI4E.Memory.Compatibility;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    /// <summary>
    /// Represents the address of a logical end point.
    /// </summary>
    [Serializable, JsonConverter(typeof(EndPointAddressJsonConverter))]
    public readonly struct EndPointAddress : IEquatable<EndPointAddress>, ISerializable
    {
        public static EndPointAddress UnknownAddress { get; } = default;

        #region C'tor

        public EndPointAddress(string logicalAddress)
        {
            if (string.IsNullOrWhiteSpace(logicalAddress))
            {
                this = UnknownAddress;
            }
            else
            {
                Utf8EncodedValue = EncodeAddress(logicalAddress.AsSpan());
            }
        }

        public EndPointAddress(ReadOnlySpan<char> logicalAddress)
        {
            if (logicalAddress.IsEmptyOrWhiteSpace())
            {
                this = UnknownAddress;
            }
            else
            {
                Utf8EncodedValue = EncodeAddress(logicalAddress);
            }
        }

        public EndPointAddress(ReadOnlyMemory<byte> utf8EncodedValue)
        {
            // TODO: Do we have to trim?
            Utf8EncodedValue = utf8EncodedValue;
        }

        private EndPointAddress(SerializationInfo info, StreamingContext context)
        {
            var logicalAddress = info.GetString("address-string");

            if (string.IsNullOrWhiteSpace(logicalAddress))
            {
                this = UnknownAddress;
            }
            else
            {
                Utf8EncodedValue = EncodeAddress(logicalAddress.AsSpan());
            }
        }

        private static ReadOnlyMemory<byte> EncodeAddress(ReadOnlySpan<char> logicalAddress)
        {
            var chars = logicalAddress.Trim();
            var byteCount = Encoding.UTF8.GetByteCount(chars);
            var bytes = new byte[byteCount];
            var bytesWritten = Encoding.UTF8.GetBytes(chars, bytes);
            Assert(bytesWritten == byteCount);
            return bytes;
        }

        #endregion

        public ReadOnlyMemory<byte> Utf8EncodedValue { get; }

        /// <summary>
        /// Returns a boolean value indicating whether the specifies end point address equals the current instance.
        /// </summary>
        /// <param name="other">The end point address to compare to.</param>
        /// <returns>True if <paramref name="other"/> equals the current end point address, false otherwise.</returns>
        public bool Equals(EndPointAddress other)
        {
            return other.Utf8EncodedValue.Span.SequenceEqual(Utf8EncodedValue.Span);
        }

        /// <summary>
        /// Return a boolean value indicating whether the specifies object equals the current end point address.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>True if <paramref name="obj"/> is of type <see cref="EndPointAddress"/> and equals the current end point address, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is EndPointAddress endPointAddress && Equals(endPointAddress);
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>The generated hash code.</returns>
        public override int GetHashCode()
        {
            return Utf8EncodedValue.SequenceHashCode();
        }

        /// <summary>
        /// Returns a stringified version of the end point address.
        /// </summary>
        /// <returns>A string representing the current end point address.</returns>
        public override string ToString()
        {
            if (Utf8EncodedValue.IsEmpty)
                return null;

            return Encoding.UTF8.GetString(Utf8EncodedValue.Span);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two end point addresses are equal.
        /// </summary>
        /// <param name="left">The first end point address.</param>
        /// <param name="right">The second end point address.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(EndPointAddress left, EndPointAddress right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two end point addresses are inequal.
        /// </summary>
        /// <param name="left">The first end point address.</param>
        /// <param name="right">The second end point address.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(EndPointAddress left, EndPointAddress right)
        {
            return !left.Equals(right);
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("address-string", ToString());
        }
    }

    public sealed class EndPointAddressJsonConverter : JsonConverter<EndPointAddress>
    {
        public override void WriteJson(JsonWriter writer, EndPointAddress value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override EndPointAddress ReadJson(JsonReader reader, Type objectType, EndPointAddress existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var addressString = reader.ReadAsString();

            if (string.IsNullOrWhiteSpace(addressString))
            {
                return EndPointAddress.UnknownAddress;
            }

            return new EndPointAddress(addressString);
        }
    }

    public static class EndPointAddressExtension
    {
        public static EndPointAddress ReadEndPointAddress(this BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            var localEndPointBytesLenght = reader.ReadInt32();

            if (localEndPointBytesLenght == 0)
            {
                return EndPointAddress.UnknownAddress;
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

                return new EndPointAddress(memory.Slice(position, localEndPointBytesLenght));
            }

            var utf8EncodedValue = new byte[localEndPointBytesLenght];
            stream.ReadExact(utf8EncodedValue, offset: 0, count: localEndPointBytesLenght);

            return new EndPointAddress(utf8EncodedValue);
        }

        public static void Write(this BinaryWriter writer, EndPointAddress endPointAddress)
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
