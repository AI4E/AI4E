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

namespace AI4E.Internal
{
    internal static class IListExtensions
    {
        private static object _lock = new object();
        private static int _seed = Environment.TickCount;
        [ThreadStatic] private static Random _rnd;

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
            lock (_lock)
            {
                unchecked
                {
                    _seed += 1;

                    if (_seed < 0)
                        _seed = 1;
                }
                return _seed;
            }
        }

        // https://stackoverflow.com/questions/6165379/quickest-way-to-randomly-re-order-a-linq-collection
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list.Count < 2)
                return;

            for (var i = list.Count; i > 1; i--)
            {
                var k = Rnd.Next(i);

                if (k == (i - 1))
                    continue;

                var value = list[k];
                list[k] = list[i - 1];
                list[i - 1] = list[k];
            }
        }
    }
}
