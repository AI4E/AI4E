using System;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static partial class MemoryExtensions
    {
        private static readonly int _scalarMultiplicationValue = 314159;
        private static readonly ReadOnlyMemory<int> _scalarMultiplicationValuePowers = BuildScalarMultiplicationValuePowers();
        private static readonly Vector<int> _scalarMultiplicator = BuildScalarMultiplicator();
        private static readonly Vector<int> _vectorMultiplicator = BuildVectorMultiplicator();
        private static readonly Type _runtimeHelpersType = typeof(RuntimeHelpers);
        private static readonly MethodInfo _isReferenceOrContainsReferencesMethodDefinition = GetIsReferenceOrContainsReferencesMethodDefinition();
        private static readonly MethodInfo _fastSequenceHashCodeMethodDefinition = GetFastSequenceHashCodeMethodDefinition();

        public static ref Vector<T> AsVectorRef<T>(this Span<T> span) where T : unmanaged
        {
            if (span.Length < Vector<T>.Count)
                throw new ArgumentException("The spans length must be at least the size of a single vector.", nameof(span));

            return ref MemoryMarshal.Cast<T, Vector<T>>(span).GetPinnableReference();
        }

        [Obsolete("Use SequenceHashCode(ReadOnlySpan<T>)")]
        public static int SequenceHashCode<T>(this ReadOnlyMemory<T> memory)
        {
            return memory.Span.SequenceHashCode();
        }

        public static int SequenceHashCode<T>(this ReadOnlySpan<T> span)
        {
            if (span.IsEmpty)
                return 0;

            if (!IsReferenceOrContainsReferences<T>())
            {
                Assert(TypeCache<T>._fastSequenceHashCode != null);
                return TypeCache<T>._fastSequenceHashCode(span);
            }

            return SequenceHashCodeInternal(span);
        }

        private static int FastSequenceHashCode<T>(ReadOnlySpan<T> span) where T : unmanaged
        {
            Assert(!IsReferenceOrContainsReferences<T>());

            unchecked
            {              
                var ints = MemoryMarshal.Cast<T, int>(span);
                var intsLength = ints.Length;
                var accumulator = 0;

                if (Vector.IsHardwareAccelerated && ints.Length >= Vector<int>.Count)
                {
                    var vectors = MemoryMarshal.Cast<int, Vector<int>>(ints);
                    Assert(vectors.Length > 0);
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

                    Assert(ints.Length >= 0 && ints.Length < Vector<int>.Count);

                    accumulator *= _scalarMultiplicationValuePowers.Span[ints.Length];
                }

                for (var i = 0; i < ints.Length; i++)
                {
                    accumulator *= _scalarMultiplicationValue;
                    accumulator += ints[i];
                }

                var nonConsideredBytes = checked((int)((long)Unsafe.SizeOf<T>() * span.Length - 4L * intsLength));
                Assert(nonConsideredBytes >= 0 && nonConsideredBytes <= 3);

                if (nonConsideredBytes > 0)
                {
                    var bytes = MemoryMarshal.Cast<T, byte>(span);
                    bytes = bytes.Slice(bytes.Length - nonConsideredBytes);
                    Assert(bytes.Length == nonConsideredBytes);

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
            Assert(IsReferenceOrContainsReferences<T>());

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
                        vectorBuffer[j] = span[j].GetHashCode();
                    }
                    span = span.Slice(Vector<int>.Count);

                    var vectorAccumulator = vector * _scalarMultiplicator;


                    for (var i = 1; i < span.Length / Vector<int>.Count; i++)
                    {
                        // Fill vectorBuffer
                        for (var j = 0; j < Vector<int>.Count; j++)
                        {
                            vectorBuffer[j] = span[j].GetHashCode();
                        }
                        span = span.Slice(Vector<int>.Count);

                        vectorAccumulator *= _vectorMultiplicator;
                        vectorAccumulator +=  vector * _scalarMultiplicator;
                    }

                    for (var i = 0; i < Vector<int>.Count; i++)
                    {
                        accumulator += vectorAccumulator[i];
                    }

                    Assert(span.Length >= 0 && span.Length < Vector<int>.Count);

                    accumulator *= _scalarMultiplicationValuePowers.Span[span.Length];
                }

                for (var i = 0; i < span.Length; i++)
                {
                    accumulator *= _scalarMultiplicationValue;
                    accumulator += span[i].GetHashCode();
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
                result[i] = result[i - 1] * _scalarMultiplicationValue;
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

            for (var i = Vector<int>.Count - 2; i <= 0; i--)
            {
                values[i] = values[i + 1] * _scalarMultiplicationValue;
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
            return typeof(MemoryExtensions).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic)
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

            internal static FastSequenceHashCodeInvoker<T> _fastSequenceHashCode = GetFastSequenceHashCodeInvoker();

            private static FastSequenceHashCodeInvoker<T> GetFastSequenceHashCodeInvoker()
            {
                if (_isReferenceOrContainsReferences)
                {
                    return null;
                }

                Assert(_fastSequenceHashCodeMethodDefinition != null);
                Assert(_fastSequenceHashCodeMethodDefinition.GetGenericArguments().Length == 1);
                var fastSequenceHashCodeMethod = _fastSequenceHashCodeMethodDefinition.MakeGenericMethod(typeof(T));
                Assert(fastSequenceHashCodeMethod.ReturnType == typeof(int));

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
                    Assert(_isReferenceOrContainsReferencesMethodDefinition.GetGenericArguments().Length == 1);
                    var isReferenceOrContainsReferencesMethod = _isReferenceOrContainsReferencesMethodDefinition.MakeGenericMethod(type);
                    Assert(isReferenceOrContainsReferencesMethod.ReturnType == typeof(bool));

                    return (bool)isReferenceOrContainsReferencesMethod.Invoke(obj: null, parameters: null);
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
    }
}
