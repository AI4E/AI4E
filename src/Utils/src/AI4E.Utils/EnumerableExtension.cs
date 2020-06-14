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
using System.Collections.Generic;
using System.Linq;
using System.Threading;

#if DEBUG
using System.Diagnostics;
#endif

namespace System.Linq
{
    /// <summary>
    /// Contains extension methods for the <see cref="IEnumerable{T}"/> type.
    /// </summary>
    public static class AI4EUtilsEnumerableExtension
    {
        #region Shuffle

        [ThreadStatic]
        private static Random? _rnd;
        private static int _count = 0;

        // https://stackoverflow.com/questions/6165379/quickest-way-to-randomly-re-order-a-linq-collection
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            var list = source.ToArray();

            if (list.Length < 2)
                return list;

            for (var i = list.Length; i > 1; i--)
            {
                var k = Rnd.Next(i);

                if (k == i - 1)
                    continue;

                Swap(ref list[k], ref list[i - 1]);
            }

#if DEBUG

            Debug.Assert(source.Count() == list.Length);

#pragma warning disable CA1062
            foreach (var element in source)
#pragma warning restore CA1062
            {
                Debug.Assert(list.Contains(element));
            }

#endif

            return list;
        }

        private static void Swap<T>(ref T left, ref T right)
        {
            var t = left;

            left = right;
            right = t;
        }

        private static Random Rnd
        {
            get
            {
                if (_rnd == null)
                {
                    var seed = GetNextSeed();

                    _rnd = new Random(seed);
                }

                return _rnd;
            }
        }

        private static int GetNextSeed()
        {
            var count = Interlocked.Increment(ref _count);

            unchecked
            {
                return count + Environment.TickCount;
            }
        }

        #endregion

        public static T FirstOrDefault<T>(this IEnumerable<T> collection, Func<T, bool> predicate, T defaultValue)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            foreach (var entry in collection)
            {
                if (predicate(entry))
                    return entry;
            }

            return defaultValue;
        }

        public static T FirstOrDefault<T>(this IEnumerable<T> collection, T defaultValue)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            using (var enumerator = collection.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    return enumerator.Current;
                }
            }

            return defaultValue;
        }

        // https://stackoverflow.com/questions/1779129/how-to-take-all-but-the-last-element-in-a-sequence-using-linq
        public static IEnumerable<T> TakeAllButLast<T>(this IEnumerable<T> source)
        {
#pragma warning disable CA1062
            var enumerator = source.GetEnumerator();
            var isFirst = true;
            T item = default!;

            try
            {
#pragma warning restore CA1062
                bool hasRemainingItems;
                do
                {
                    hasRemainingItems = enumerator.MoveNext();
                    if (hasRemainingItems)
                    {
                        if (!isFirst)
                            yield return item;
                        item = enumerator.Current;
                        isFirst = false;
                    }
                } while (hasRemainingItems);
            }
            finally
            {
                enumerator?.Dispose();
            }
        }

#if NETSTD20
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source is HashSet<T> hashSet)
            {
                return hashSet;
            }

            return new HashSet<T>(source);
        }
#endif

        private const int _sequenceHashCodeSeedValue = 0x2D2816FE;
        private const int _sequenceHashCodePrimeNumber = 397;

        // Adapted from: https://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order#answer-48192420
        public static int SequenceHashCode<TItem>(this IEnumerable<TItem> list)
        {
            if (list == null)
                return 0;

            static int Aggregation(int current, TItem item)
            {
                return current * _sequenceHashCodePrimeNumber + (item is null ? 0 : item.GetHashCode());
            }

            return list.Aggregate(_sequenceHashCodeSeedValue, Aggregation);
        }

        public static bool All(this IEnumerable<bool> enumerable)
        {
            return enumerable.All(_ => _);
        }

        #region TopologicalSort

        // Based on: https://stackoverflow.com/questions/4106862/how-to-sort-depended-objects-by-dependency#answer-11027096

        /// <summary>
        /// Topologically sorts the source elements.
        /// </summary>
        /// <typeparam name="T">The type of element.</typeparam>
        /// <param name="source">The enumerable of source elements.</param>
        /// <param name="dependencies">
        /// A func that returns the dependencies of the specified elements.
        /// </param>
        /// <param name="throwOnCycle">
        /// A boolean value that indicates whether an exception shall be thrown when cycles are detected.
        /// </param>
        /// <returns>The topologically sorted source elements.</returns>
        /// <remarks>
        /// The sort is stable. Elements that are on the same level in the topology are guaranteed to be in the same
        /// order than they were in the source collection.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dependencies"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if a cycle is detected and <paramref name="throwOnCycle"/> is true.
        /// </exception>
        public static IEnumerable<T> TopologicalSort<T>(
            this IEnumerable<T> source,
            Func<T, IEnumerable<T>> dependencies,
            bool throwOnCycle = false)
        {
            var sorted = new List<T>();
            var visited = new HashSet<T>();

#pragma warning disable CA1062
            foreach (var item in source)
#pragma warning restore CA1062
            {
                Visit(item, visited, sorted, dependencies, throwOnCycle);
            }

            return sorted;
        }

        private static void Visit<T>(
            T item,
            HashSet<T> visited,
            List<T> sorted,
            Func<T, IEnumerable<T>> dependencies,
            bool throwOnCycle)
        {
            if (!visited.Contains(item))
            {
                visited.Add(item);

                foreach (var dep in dependencies(item))
                {
                    Visit(dep, visited, sorted, dependencies, throwOnCycle);
                }

                sorted.Add(item);
            }
            else
            {
                if (throwOnCycle && !sorted.Contains(item))
                {
                    throw new InvalidOperationException("Cyclic dependency found.");
                }
            }
        }

        #endregion
    }
}
