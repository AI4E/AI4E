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
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AI4E.Utils;
using System.Diagnostics;

namespace System.IO
{
    public static class AI4EUtilsMemoryCompatibilityBinaryWriterExtensions
    {
        private static readonly WriteBytesShim? _writeBytesShim = BuildWriteBytesShim(typeof(BinaryWriter));
        private static readonly WriteCharsShim? _writeCharsShim = BuildWriteCharsShim(typeof(BinaryWriter));

        private static WriteBytesShim? BuildWriteBytesShim(Type binaryWriterType)
        {
            var writeMethod = binaryWriterType.GetMethod(nameof(Write), new[] { typeof(ReadOnlySpan<byte>) });

            if (writeMethod == null)
                return null;

            Debug.Assert(writeMethod.ReturnType == typeof(void));

            var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
            var bufferParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "buffer");
            var methodCall = Expression.Call(binaryWriterParameter, writeMethod, bufferParameter);
            return Expression.Lambda<WriteBytesShim>(methodCall, binaryWriterParameter, bufferParameter).Compile();
        }

        private static WriteCharsShim? BuildWriteCharsShim(Type binaryWriterType)
        {
            var writeMethod = binaryWriterType.GetMethod(nameof(Write), new[] { typeof(ReadOnlySpan<char>) });

            if (writeMethod == null)
                return null;

            Debug.Assert(writeMethod.ReturnType == typeof(void));

            var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
            var charsParameter = Expression.Parameter(typeof(ReadOnlySpan<char>), "chars");
            var methodCall = Expression.Call(binaryWriterParameter, writeMethod, charsParameter);
            return Expression.Lambda<WriteCharsShim>(methodCall, binaryWriterParameter, charsParameter).Compile();
        }

        private delegate void WriteBytesShim(BinaryWriter writer, ReadOnlySpan<byte> buffer);
        private delegate void WriteCharsShim(BinaryWriter writer, ReadOnlySpan<char> chars);

        public static void Write(this BinaryWriter writer, ReadOnlySpan<byte> buffer)
        {
            if (_writeBytesShim != null)
            {
                _writeBytesShim(writer, buffer);
                return;
            }

#pragma warning disable CA1062
            writer.Flush();
#pragma warning restore CA1062

            var underlyingStream = writer.BaseStream;
            Debug.Assert(underlyingStream != null);
            underlyingStream!.Write(buffer);
        }

        public static void Write(this BinaryWriter writer, ReadOnlySpan<char> chars)
        {
            if (_writeCharsShim != null)
            {
                _writeCharsShim(writer, chars);
                return;
            }

            var encoding = TryGetEncoding(writer);

            if (encoding != null)
            {
                var array = ArrayPool<byte>.Shared.Rent(encoding.GetByteCount(chars));
                try
                {
                    var byteCount = encoding.GetBytes(chars, array);

                    PrefixCodingHelper.Write7BitEncodedInt(writer, byteCount);
                    writer.Write(array, index: 0, byteCount);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(array);
                }
            }

            var str = new string('\0', chars.Length);
            chars.CopyTo(MemoryMarshal.AsMemory(str.AsMemory()).Span);

            writer.Write(str);
        }

        private static readonly Lazy<Func<BinaryWriter, Encoding?>> _encodingLookupLazy
            = new Lazy<Func<BinaryWriter, Encoding?>>(BuildEncodingLookup, LazyThreadSafetyMode.PublicationOnly);

        private static Encoding? TryGetEncoding(BinaryWriter writer)
        {
            return _encodingLookupLazy.Value(writer);
        }

        private static Func<BinaryWriter, Encoding?> BuildEncodingLookup()
        {
            var binaryWriterType = typeof(BinaryWriter);
            var encodingType = typeof(Encoding);

            var encodingField = binaryWriterType.GetField("_encoding", BindingFlags.Instance | BindingFlags.NonPublic);

            // corefx
            if (encodingField != null && encodingField.FieldType == encodingType)
            {
                var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
                var fieldAccess = Expression.MakeMemberAccess(binaryWriterParameter, encodingField);
                return Expression.Lambda<Func<BinaryWriter, Encoding>>(fieldAccess, binaryWriterParameter).Compile();
            }

            var decoderType = typeof(Decoder);
            var decoderField = binaryWriterType.GetField("m_decoder", BindingFlags.Instance | BindingFlags.NonPublic);

            // .Net Framework
            if (decoderField != null && decoderField.FieldType == decoderType)
            {
                var defaultDecoderType = Type.GetType("System.Text.Encoding.DefaultDecoder, mscorlib", throwOnError: false);

                if (defaultDecoderType == null)
                    return _ => null;

                encodingField = defaultDecoderType.GetField("m_encoding", BindingFlags.Instance | BindingFlags.NonPublic);

                if (encodingField == null || encodingField.FieldType != encodingType)
                    return _ => null;

                var binaryWriterParameter = Expression.Parameter(binaryWriterType, "writer");
                var decoderFieldAccess = Expression.MakeMemberAccess(binaryWriterParameter, decoderField);
                var isDefaultDecoder = Expression.TypeIs(decoderFieldAccess, defaultDecoderType);


                var decoderConvert = Expression.Convert(decoderFieldAccess, defaultDecoderType);
                var encodingFieldAccess = Expression.MakeMemberAccess(decoderConvert, encodingField);

                var nullConstant = Expression.Constant(null, typeof(Encoding));
                var result = Expression.Condition(isDefaultDecoder, encodingFieldAccess, nullConstant);

                return Expression.Lambda<Func<BinaryWriter, Encoding>>(result, binaryWriterParameter).Compile();
            }

            return _ => null;
        }
    }
}
