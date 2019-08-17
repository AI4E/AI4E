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
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination.Session
{
    /// <summary>
    /// Represents a coordination session.
    /// </summary>
    public readonly struct SessionIdentifier : IEquatable<SessionIdentifier>
    {
        private readonly ReadOnlyMemory<byte> _bytes;

        /// <summary>
        /// Creates a new <see cref="SessionIdentifier"/> with the specified prefix and physical address.
        /// </summary>
        /// <param name="prefix">A span of bytes that represent the prefix.</param>
        /// <param name="physicalAddress">A span of bytes that represent the physical address.</param>
        public SessionIdentifier(ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> physicalAddress)
        {
            if (physicalAddress.IsEmpty)
            {
                this = default;
                return;
            }

            var bytes = (new byte[4 + prefix.Length + physicalAddress.Length]).AsMemory();
            BinaryPrimitives.WriteInt32LittleEndian(bytes.Span, prefix.Length);

            prefix.CopyTo(bytes.Span.Slice(start: 4));
            physicalAddress.CopyTo(bytes.Span.Slice(start: 4 + prefix.Length));

            _bytes = bytes;
        }

        private SessionIdentifier(ReadOnlyMemory<byte> bytes)
        {
            Assert(!bytes.IsEmpty);

            _bytes = bytes;
        }

        /// <summary>
        /// Gets a memory of bytes that represent the session prefix.
        /// </summary>
        public ReadOnlyMemory<byte> Prefix => _bytes.IsEmpty ? _bytes : _bytes.Slice(start: 4, length: BinaryPrimitives.ReadInt32LittleEndian(_bytes.Span));

        /// <summary>
        /// Gets a memory of bytes that represent the session's physical address.
        /// </summary>
        public ReadOnlyMemory<byte> PhysicalAddress => _bytes.IsEmpty ? _bytes : _bytes.Slice(start: 4 + BinaryPrimitives.ReadInt32LittleEndian(_bytes.Span));

        /// <inheritdoc/>
        public bool Equals(SessionIdentifier other)
        {
            return _bytes.Span.SequenceEqual(other._bytes.Span);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is SessionIdentifier session && Equals(session);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _bytes.Span.SequenceHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (_bytes.IsEmpty)
                return string.Empty;

            var resultLenght = Base64Coder.ComputeBase64EncodedLength(_bytes.Span);
            var result = new string('\0', resultLenght);
            var memory = MemoryMarshal.AsMemory(result.AsMemory());

            var writtenMemory = Base64Coder.ToBase64Chars(_bytes.Span, memory.Span);
            Assert(writtenMemory.Length == resultLenght);

            return result;
        }

        /// <summary>
        /// Compares two <see cref="SessionIdentifier"/>s.
        /// </summary>
        /// <param name="left">The first segment.</param>
        /// <param name="right">The second segment.</param>
        /// <returns>True, if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(SessionIdentifier left, SessionIdentifier right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="SessionIdentifier"/>s.
        /// </summary>
        /// <param name="left">The first segment.</param>
        /// <param name="right">The second segment.</param>
        /// <returns>True, if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(SessionIdentifier left, SessionIdentifier right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates a <see cref="SessionIdentifier"/> from the specified span of chars.
        /// </summary>
        /// <param name="chars">The span of chars to create the session from.</param>
        /// <returns>The <see cref="SessionIdentifier"/> that is created from <paramref name="chars"/>.</returns>
        public static SessionIdentifier FromChars(ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty)
                return default;

            var bytes = new byte[Base64Coder.ComputeBase64DecodedLength(chars)];
            var bytesLength = Base64Coder.FromBase64Chars(chars, bytes).Length;

            return new SessionIdentifier(bytes.AsMemory().Slice(start: 0, length: bytesLength));
        }

        /// <summary>
        /// Creates a <see cref="SessionIdentifier"/> from the specified string.
        /// </summary>
        /// <param name="str">The string to create the session from.</param>
        /// <returns>The <see cref="SessionIdentifier"/> that is created from <paramref name="str"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="str"/> is <c>null</c>.</exception>
        public static SessionIdentifier FromString(string str)
        {
            if (str == null)
                throw new ArgumentNullException(nameof(str));

            return FromChars(str.AsSpan());
        }
    }
}
