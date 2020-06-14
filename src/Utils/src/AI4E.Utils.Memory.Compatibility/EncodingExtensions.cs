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

using System.Linq.Expressions;
using static System.Diagnostics.Debug;

namespace System.Text
{
    public static class AI4EUtilsMemoryCompatibilityEncodingExtensions
    {
        private static readonly GetByteCountShim? _getByteCountShim = BuildGetByteCountShim(typeof(Encoding));
        private static readonly GetBytesShim? _getBytesShim = BuildGetBytesShim(typeof(Encoding));
        private static readonly GetCharCountShim? _getCharCountShim = BuildGetCharCountShim(typeof(Encoding));
        private static readonly GetCharsShim? _getCharsShim = BuildGetCharsShim(typeof(Encoding));
        private static readonly GetStringShim? _getStringShim = BuildGetStringShim(typeof(Encoding));

        private static GetByteCountShim? BuildGetByteCountShim(Type encodingType)
        {
            var getByteCountMethod = encodingType.GetMethod(nameof(Encoding.GetByteCount), new[] { typeof(ReadOnlySpan<char>) });

            if (getByteCountMethod == null)
                return null;

            Assert(getByteCountMethod.ReturnType == typeof(int));

            var encodingParameter = Expression.Parameter(typeof(Encoding), "encoding");
            var charsParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "chars");
            var methodCall = Expression.Call(encodingParameter, getByteCountMethod, charsParameter);
            return Expression.Lambda<GetByteCountShim>(methodCall, encodingParameter, charsParameter).Compile();
        }

        private static GetBytesShim? BuildGetBytesShim(Type encodingType)
        {
            var getBytesMethod = encodingType.GetMethod(nameof(Encoding.GetBytes), new[] { typeof(ReadOnlySpan<char>), typeof(Span<byte>) });

            if (getBytesMethod == null)
                return null;

            Assert(getBytesMethod.ReturnType == typeof(int));

            var encodingParameter = Expression.Parameter(typeof(Encoding), "encoding");
            var charsParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "chars");
            var bytesParameter = Expression.Parameter(typeof(Span<byte>), "bytes");
            var methodCall = Expression.Call(encodingParameter, getBytesMethod, charsParameter, bytesParameter);
            return Expression.Lambda<GetBytesShim>(methodCall, encodingParameter, charsParameter, bytesParameter).Compile();
        }

        private static GetCharCountShim? BuildGetCharCountShim(Type encodingType)
        {
            var getCharCountMethod = encodingType.GetMethod(nameof(Encoding.GetCharCount), new[] { typeof(ReadOnlySpan<byte>) });

            if (getCharCountMethod == null)
                return null;

            Assert(getCharCountMethod.ReturnType == typeof(int));

            var encodingParameter = Expression.Parameter(typeof(Encoding), "encoding");
            var bytesParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "bytes");
            var methodCall = Expression.Call(encodingParameter, getCharCountMethod, bytesParameter);
            return Expression.Lambda<GetCharCountShim>(methodCall, encodingParameter, bytesParameter).Compile();
        }

        private static GetCharsShim? BuildGetCharsShim(Type encodingType)
        {
            var getCharsMethod = encodingType.GetMethod(nameof(Encoding.GetChars), new[] { typeof(ReadOnlySpan<byte>), typeof(Span<char>) });

            if (getCharsMethod == null)
                return null;

            Assert(getCharsMethod.ReturnType == typeof(int));

            var encodingParameter = Expression.Parameter(typeof(Encoding), "encoding");
            var bytesParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "bytes");
            var charsParameter = Expression.Parameter(typeof(Span<char>), "chars");
            var methodCall = Expression.Call(encodingParameter, getCharsMethod, bytesParameter, charsParameter);
            return Expression.Lambda<GetCharsShim>(methodCall, encodingParameter, bytesParameter, charsParameter).Compile();
        }

        private static GetStringShim? BuildGetStringShim(Type encodingType)
        {
            var getStringMethod = encodingType.GetMethod(nameof(Encoding.GetString), new[] { typeof(ReadOnlySpan<byte>) });

            if (getStringMethod == null)
                return null;

            Assert(getStringMethod.ReturnType == typeof(string));

            var encodingParameter = Expression.Parameter(typeof(Encoding), "encoding");
            var bytesParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "bytes");
            var methodCall = Expression.Call(encodingParameter, getStringMethod, bytesParameter);
            return Expression.Lambda<GetStringShim>(methodCall, encodingParameter, bytesParameter).Compile();
        }

        private delegate int GetByteCountShim(Encoding encoding, ReadOnlySpan<char> chars);
        private delegate int GetBytesShim(Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes);
        private delegate int GetCharCountShim(Encoding encoding, ReadOnlySpan<byte> bytes);
        private delegate int GetCharsShim(Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars);
        private delegate string GetStringShim(Encoding encoding, ReadOnlySpan<byte> bytes);

        public static int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (_getByteCountShim != null)
            {
                return _getByteCountShim(encoding, chars);
            }

            unsafe
            {
                fixed (char* charsPtr = chars)
                {
                    return encoding.GetByteCount(charsPtr, chars.Length);
                }
            }
        }

        public static int GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, Span<byte> bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (_getBytesShim != null)
            {
                return _getBytesShim(encoding, chars, bytes);
            }

            unsafe
            {
                fixed (char* charsPtr = chars)
                fixed (byte* bytesPtr = bytes)
                {
                    return encoding.GetBytes(charsPtr, chars.Length, bytesPtr, bytes.Length);
                }
            }
        }

        public static int GetCharCount(this Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (_getCharCountShim != null)
            {
                return _getCharCountShim(encoding, bytes);
            }

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                {
                    return encoding.GetCharCount(bytesPtr, bytes.Length);
                }
            }
        }

        public static int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (_getCharsShim != null)
            {
                return _getCharsShim(encoding, bytes, chars);
            }

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                fixed (char* charsPtr = chars)
                {
                    return encoding.GetChars(bytesPtr, bytes.Length, charsPtr, chars.Length);
                }
            }
        }

        public static string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (_getStringShim != null)
            {
                return _getStringShim(encoding, bytes);
            }

            unsafe
            {
                fixed (byte* bytesPtr = bytes)
                {
                    return encoding.GetString(bytesPtr, bytes.Length);
                }
            }
        }
    }
}
