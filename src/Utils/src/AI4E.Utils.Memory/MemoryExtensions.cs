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

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AI4E.Utils;
using AI4E.Utils.Memory;

namespace System
{
    public static partial class AI4EUtilsMemoryMemoryExtensions
    {
        /// <summary>
        /// Indicates whether the specified span is empty or contains only white-space characters.
        /// </summary>
        public static bool IsEmptyOrWhiteSpace(this ReadOnlySpan<char> span)
        {
            return span.IsEmpty || span.IsWhiteSpace();
        }

#if NETSTD20 || NETSTD21

        // TODO: Add other Trim methods from COREFX

        /// <summary>
        /// Removes all leading and trailing white-space characters from the memory.
        /// </summary>
        /// <param name="memory">The source memory from which the characters are removed.</param>
        public static ReadOnlyMemory<char> Trim(this ReadOnlyMemory<char> memory)
        {
            var span = memory.Span;
            var start = ClampStart(span);
            var length = ClampEnd(span, start);
            return memory.Slice(start, length);
        }

        /// <summary>
        /// Delimits all leading occurrences of whitespace charecters from the span.
        /// </summary>
        /// <param name="span">The source span from which the characters are removed.</param>
        private static int ClampStart(ReadOnlySpan<char> span)
        {
            var start = 0;

            for (; start < span.Length; start++)
            {
                if (!char.IsWhiteSpace(span[start]))
                {
                    break;
                }
            }

            return start;
        }

        /// <summary>
        /// Delimits all trailing occurrences of whitespace charecters from the span.
        /// </summary>
        /// <param name="span">The source span from which the characters are removed.</param>
        /// <param name="start">The start index from which to being searching.</param>
        private static int ClampEnd(ReadOnlySpan<char> span, int start)
        {
            // Initially, start==len==0. If ClampStart trims all, start==len
            Debug.Assert((uint)start <= span.Length);

            var end = span.Length - 1;

            for (; end >= start; end--)
            {
                if (!char.IsWhiteSpace(span[end]))
                {
                    break;
                }
            }

            return end - start + 1;
        }

#endif

        public static string ConvertToString(this ReadOnlyMemory<char> memory)
        {
            if (memory.IsEmpty)
            {
                return string.Empty;
            }

            if (MemoryMarshal.TryGetString(memory, out var text, out var start, out var length))
            {
                // If the memory is only a part of the string we had to substring anyway.
                if (start == 0 && length == text.Length)
                {
                    return text;
                }
            }

            var result = new string('\0', memory.Length);
            var resultAsMemory = MemoryMarshal.AsMemory(result.AsMemory());
            memory.CopyTo(resultAsMemory);
            return result;
        }

        public static string InternAsString(this ReadOnlyMemory<char> memory)
        {
            return MemoryInterning<char>.Instance.InternAsString(memory);
        }

        public static T[] CopyToArray<T>(in this ReadOnlySpan<T> span)
        {
            var result = new T[span.Length];
            span.CopyTo(result);
            return result;
        }

        public static T[] CopyToArray<T>(in this ReadOnlyMemory<T> memory)
        {
            return memory.Span.CopyToArray();
        }

        public static SlicedMemoryOwner<byte> Base64Decode(this string str, MemoryPool<byte> memoryPool)
        {
            return Base64Decode(str.AsSpan(), memoryPool);
        }

        public static SlicedMemoryOwner<byte> Base64Decode(in this ReadOnlySpan<char> chars, MemoryPool<byte> memoryPool)
        {
            var minBytesLength = Base64Coder.ComputeBase64DecodedLength(chars);
            var bytesOwner = memoryPool.RentExact(minBytesLength);

            try
            {
                var success = Base64Coder.TryFromBase64Chars(chars, bytesOwner.Memory.Span, out var bytesWritten);
                Debug.Assert(success);

                return bytesOwner.Slice(start: 0, length: bytesWritten);
            }
            catch
            {
                bytesOwner.Dispose();
                throw;
            }
        }

        public static ref Vector<T> AsVectorRef<T>(this Span<T> span) where T : unmanaged
        {
            if (span.Length < Vector<T>.Count)
                throw new ArgumentException("The spans length must be at least the size of a single vector.", nameof(span));

            return ref MemoryMarshal.Cast<T, Vector<T>>(span).GetPinnableReference();
        }

        public static PooledMemoryStream AsStream(in this ReadOnlyMemory<byte> memory)
        {
            return new PooledMemoryStream(memory);
        }

        #region SequenceHashCode

        private const int _scalarMultiplicationValue = 314159;
        private static readonly ReadOnlyMemory<int> _scalarMultiplicationValuePowers = BuildScalarMultiplicationValuePowers();
        private static readonly Vector<int> _scalarMultiplicator = BuildScalarMultiplicator();
        private static readonly Vector<int> _vectorMultiplicator = BuildVectorMultiplicator();
        private static readonly Type _runtimeHelpersType = typeof(RuntimeHelpers);
        private static readonly MethodInfo _isReferenceOrContainsReferencesMethodDefinition = GetIsReferenceOrContainsReferencesMethodDefinition();
        private static readonly MethodInfo _fastSequenceHashCodeMethodDefinition = GetFastSequenceHashCodeMethodDefinition();

        public static int SequenceHashCode<T>(this Span<T> span)
        {
            return SequenceHashCode((ReadOnlySpan<T>)span);
        }

        public static int SequenceHashCode<T>(this ReadOnlySpan<T> span)
        {
            if (span.IsEmpty)
                return 0;

            if (!IsReferenceOrContainsReferences<T>())
            {
                Debug.Assert(TypeCache<T>._fastSequenceHashCode != null);
                return TypeCache<T>._fastSequenceHashCode!.Invoke(span);
            }

            return SequenceHashCodeInternal(span);
        }

        private static int FastSequenceHashCode<T>(ReadOnlySpan<T> span) where T : unmanaged
        {
            Debug.Assert(!IsReferenceOrContainsReferences<T>());

            unchecked
            {
                var ints = MemoryMarshal.Cast<T, int>(span);
                var intsLength = ints.Length;
                var accumulator = 0;

                if (Vector.IsHardwareAccelerated && ints.Length >= Vector<int>.Count)
                {
                    var vectors = MemoryMarshal.Cast<int, Vector<int>>(ints);
                    Debug.Assert(vectors.Length > 0);
                    var vectorAccumulator = vectors[0] * _scalarMultiplicator;

                    for (var i = 1; i < vectors.Length; i++)
                    {
                        vectorAccumulator *= _vectorMultiplicator;
                        vectorAccumulator += vectors[i] * _scalarMultiplicator;
                    }

                    for (var i = 0; i < Vector<int>.Count; i++)
                    {
                        accumulator += vectorAccumulator[i];
                    }

                    ints = ints.Slice(Vector<int>.Count * vectors.Length);

                    Debug.Assert(ints.Length >= 0 && ints.Length < Vector<int>.Count);

                    accumulator *= _scalarMultiplicationValuePowers.Span[ints.Length];
                }

                for (var i = 0; i < ints.Length; i++)
                {
                    accumulator *= _scalarMultiplicationValue;
                    accumulator += ints[i];
                }

                var nonConsideredBytes = checked((int)((long)Unsafe.SizeOf<T>() * span.Length - 4L * intsLength));
                Debug.Assert(nonConsideredBytes >= 0 && nonConsideredBytes <= 3);

                if (nonConsideredBytes > 0)
                {
                    var bytes = MemoryMarshal.Cast<T, byte>(span);
                    bytes = bytes.Slice(bytes.Length - nonConsideredBytes);
                    Debug.Assert(bytes.Length == nonConsideredBytes);

                    var x = (int)bytes[0];

                    for (var i = 1; i < nonConsideredBytes; i++)
                    {
                        // As the resulting hash code is not intended to be platform independent, we do not care about byte order here.
                        x = (x << 1) & bytes[i];
                    }

                    accumulator *= _scalarMultiplicationValue;
                    accumulator += x;
                }

                accumulator *= _scalarMultiplicationValue;
                accumulator += span.Length;

                return accumulator;
            }
        }

        private static int SequenceHashCodeInternal<T>(ReadOnlySpan<T> span)
        {
            Debug.Assert(IsReferenceOrContainsReferences<T>());

            unchecked
            {
                var accumulator = 0;

                if (Vector.IsHardwareAccelerated && span.Length >= Vector<int>.Count)
                {
                    Span<int> vectorBuffer = stackalloc int[Vector<int>.Count];
                    ref Vector<int> vector = ref vectorBuffer.AsVectorRef();

                    // Fill vectorBuffer
                    for (var j = 0; j < Vector<int>.Count; j++)
                    {
                        vectorBuffer[j] = span[j]?.GetHashCode() ?? 0;
                    }

                    var originalLength = span.Length;
                    span = span.Slice(Vector<int>.Count);

                    var vectorAccumulator = vector * _scalarMultiplicator;


                    for (var i = 1; i < originalLength / Vector<int>.Count; i++)
                    {
                        // Fill vectorBuffer
                        for (var j = 0; j < Vector<int>.Count; j++)
                        {
                            vectorBuffer[j] = span[j]?.GetHashCode() ?? 0;
                        }
                        span = span.Slice(Vector<int>.Count);

                        vectorAccumulator *= _vectorMultiplicator;
                        vectorAccumulator += vector * _scalarMultiplicator;
                    }

                    for (var i = 0; i < Vector<int>.Count; i++)
                    {
                        accumulator += vectorAccumulator[i];
                    }

                    Debug.Assert(span.Length >= 0 && span.Length < Vector<int>.Count);

                    accumulator *= _scalarMultiplicationValuePowers.Span[span.Length];
                }

                for (var i = 0; i < span.Length; i++)
                {
                    accumulator *= _scalarMultiplicationValue;
                    accumulator += span[i]?.GetHashCode() ?? 0;
                }

                accumulator *= _scalarMultiplicationValue;
                accumulator += span.Length;

                return accumulator;
            }
        }

        private static ReadOnlyMemory<int> BuildScalarMultiplicationValuePowers()
        {
            var result = new int[Vector<int>.Count + 1];
            result[0] = 1;

            // result[0] = x^0 = 1
            // result[1] = x^1 = x
            // ...
            // result[n] = x^n

            for (var i = 1; i <= Vector<int>.Count; i++)
            {
                result[i] = unchecked(result[i - 1] * _scalarMultiplicationValue);
            }

            return result;
        }

        private static Vector<int> BuildScalarMultiplicator()
        {
            if (!Vector.IsHardwareAccelerated)
            {
                return default;
            }

            // [ x^n ... x^2 x 1]
            var values = new int[Vector<int>.Count];
            values[Vector<int>.Count - 1] = 1;

            for (var i = Vector<int>.Count - 2; i >= 0; i--)
            {
                values[i] = unchecked(values[i + 1] * _scalarMultiplicationValue);
            }

            return new Vector<int>(values);
        }

        private static Vector<int> BuildVectorMultiplicator()
        {
            if (!Vector.IsHardwareAccelerated)
            {
                return default;
            }

            var value = _scalarMultiplicationValuePowers.Span[Vector<int>.Count];

            return new Vector<int>(value);
        }

        private static MethodInfo GetIsReferenceOrContainsReferencesMethodDefinition()
        {
            var result = _runtimeHelpersType.GetMethods(BindingFlags.Static | BindingFlags.Static)
                               .SingleOrDefault(p => p.IsGenericMethodDefinition &&
                                                     p.Name == "IsReferenceOrContainsReferences");

            return result;
        }

        private static MethodInfo GetFastSequenceHashCodeMethodDefinition()
        {
            return typeof(AI4EUtilsMemoryMemoryExtensions)
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic)
                .SingleOrDefault(p => p.Name == nameof(FastSequenceHashCode) && p.IsGenericMethodDefinition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsReferenceOrContainsReferences<T>()
        {
            return TypeCache<T>._isReferenceOrContainsReferences;
        }

        private delegate int FastSequenceHashCodeInvoker<T>(ReadOnlySpan<T> span);

        private static class TypeCache<T>
        {
            internal static readonly bool _isReferenceOrContainsReferences = IsReferenceOrContainsReferences();

            internal static FastSequenceHashCodeInvoker<T>? _fastSequenceHashCode = GetFastSequenceHashCodeInvoker();

            private static FastSequenceHashCodeInvoker<T>? GetFastSequenceHashCodeInvoker()
            {
                if (_isReferenceOrContainsReferences)
                {
                    return null;
                }

                Debug.Assert(_fastSequenceHashCodeMethodDefinition != null);
                Debug.Assert(_fastSequenceHashCodeMethodDefinition!.GetGenericArguments().Length == 1);
                var fastSequenceHashCodeMethod = _fastSequenceHashCodeMethodDefinition.MakeGenericMethod(typeof(T));
                Debug.Assert(fastSequenceHashCodeMethod.ReturnType == typeof(int));

                var memoryParameter = Expression.Parameter(typeof(ReadOnlySpan<T>), "memory");
                var methodCall = Expression.Call(fastSequenceHashCodeMethod, memoryParameter);
                var lambda = Expression.Lambda<FastSequenceHashCodeInvoker<T>>(methodCall, memoryParameter);
                return lambda.Compile();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsReferenceOrContainsReferences()
            {
                var type = typeof(T);

                if (_isReferenceOrContainsReferencesMethodDefinition != null)
                {
                    Debug.Assert(_isReferenceOrContainsReferencesMethodDefinition.GetGenericArguments().Length == 1);
                    var isReferenceOrContainsReferencesMethod = _isReferenceOrContainsReferencesMethodDefinition.MakeGenericMethod(type);
                    Debug.Assert(isReferenceOrContainsReferencesMethod.ReturnType == typeof(bool));

                    return (bool)isReferenceOrContainsReferencesMethod.Invoke(obj: null, parameters: null)!;
                }

                return IsReferenceOrContainsReferences(type);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsReferenceOrContainsReferences(Type type)
            {
                if (type.IsPointer)
                    return false;

                if (!type.IsValueType)
                    return true;

                foreach (var field in type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.FieldType != type && IsReferenceOrContainsReferences(field.FieldType))
                        return true;
                }

                return false;
            }
        }

        #endregion
    }
}
