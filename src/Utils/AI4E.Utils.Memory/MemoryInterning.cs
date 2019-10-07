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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace AI4E.Utils.Memory
{
    public static class MemoryInterning
    {
        public static MemoryInterning<T> GetInstance<T>()
            where T : IEquatable<T>
        {
            return MemoryInterning<T>.Instance;
        }
    }

#pragma warning disable CA1001
    public sealed class MemoryInterning<T>
#pragma warning restore CA1001
        where T : IEquatable<T>
    {
        private static readonly IEqualityComparer<ReadOnlyMemory<T>> _memoryEqualityComparer = new MemoryEqualityComparer();

        internal static MemoryInterning<T> Instance { get; } = new MemoryInterning<T>();

        private readonly ConcurrentDictionary<ReadOnlyMemory<T>, ReadOnlyMemory<T>> _lookup;
        private readonly ThreadLocal<Dictionary<ReadOnlyMemory<T>, ReadOnlyMemory<T>>> _tlsLookup;

        private MemoryInterning()
        {
            _lookup = new ConcurrentDictionary<ReadOnlyMemory<T>, ReadOnlyMemory<T>>(_memoryEqualityComparer);
            _tlsLookup = new ThreadLocal<Dictionary<ReadOnlyMemory<T>, ReadOnlyMemory<T>>>(() => new Dictionary<ReadOnlyMemory<T>, ReadOnlyMemory<T>>(), trackAllValues: false);
        }

        public ReadOnlyMemory<T> Intern(ReadOnlyMemory<T> memory)
        {
            var tlsLookup = _tlsLookup.Value;
            Debug.Assert(tlsLookup != null);
            if (!tlsLookup!.TryGetValue(memory, out var internedValue))
            {
                internedValue = _lookup.GetOrAdd(memory, InternValue);
                tlsLookup.Add(memory, internedValue);
            }

            return internedValue;
        }

        private ReadOnlyMemory<T> InternValue(ReadOnlyMemory<T> memory)
        {
            if (typeof(T) == typeof(char))
            {
                var charMemory = Unsafe.As<ReadOnlyMemory<T>, ReadOnlyMemory<char>>(ref memory);

                if (MemoryMarshal.TryGetString(charMemory, out var str, out var start, out var length))
                {
                    if (start == 0 && length == str.Length)
                        return memory;

                    var result = charMemory.ToString().AsMemory();
                    return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref result);
                }
                else
                {
                    var result = new string('\0', charMemory.Length).AsMemory();
                    charMemory.CopyTo(MemoryMarshal.AsMemory(result));
                    return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref result);
                }
            }

            return memory.ToArray();
        }

        private sealed class MemoryEqualityComparer : IEqualityComparer<ReadOnlyMemory<T>>
        {
            public bool Equals(ReadOnlyMemory<T> x, ReadOnlyMemory<T> y)
            {
                return x.Span.SequenceEqual(y.Span);
            }

            public int GetHashCode(ReadOnlyMemory<T> obj)
            {
                return obj.Span.SequenceHashCode();
            }
        }
    }

    public static class MemoryInterningExtension
    {
        public static string InternAsString(this MemoryInterning<char> memoryInterning, ReadOnlyMemory<char> memory)
        {
            if (memoryInterning == null)
                throw new ArgumentNullException(nameof(memoryInterning));

            var internedValue = memoryInterning.Intern(memory);
            if (!MemoryMarshal.TryGetString(internedValue, out var result, out var start, out var length) || start != 0 || result.Length != length)
            {
                Debug.Assert(false); // The MemoryInterning type guarantees that this is a string, but we provide a fallback for production for any case.

                return internedValue.ToString();
            }

            return result;
        }
    }
}
