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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class EnumerableExtension
    {
        private const int _sequenceHashCodeSeedValue = 0x2D2816FE;
        private const int _sequenceHashCodePrimeNumber = 397;

        [ThreadStatic]
        private static Random _rnd;
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

                if (k == (i - 1))
                    continue;

                Swap(ref list[k], ref list[i - 1]);
            }

#if DEBUG

            Assert(source.Count() == list.Length);

            foreach (var element in source)
            {
                Assert(list.Contains(element));
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
            var enumerator = source.GetEnumerator();
            var hasRemainingItems = false;
            var isFirst = true;
            var item = default(T);

            try
            {
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

#if NETSTANDARD
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

        // Adapted from: https://stackoverflow.com/questions/8094867/good-gethashcode-override-for-list-of-foo-objects-respecting-the-order#answer-48192420
        public static int GetSequenceHashCode<TItem>(this IEnumerable<TItem> list)
        {
            if (list == null)
                return 0;
           
            return list.Aggregate(_sequenceHashCodeSeedValue, (current, item) => (current * _sequenceHashCodePrimeNumber) + (Equals(item, default(TItem)) ? 0 : item.GetHashCode()));
        }

    }
}
