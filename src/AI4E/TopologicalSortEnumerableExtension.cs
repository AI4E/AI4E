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

// Based on: https://stackoverflow.com/questions/4106862/how-to-sort-depended-objects-by-dependency#answer-11027096

using System.Collections.Generic;

namespace System.Linq
{
    internal static class TopologicalSortEnumerableExtension // TODO: Move this to AI4E.Utils and change the access modifier to public
    {
        public static IEnumerable<T> TopologicalSort<T>(
            this IEnumerable<T> source, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle = false)
        {
            var sorted = new List<T>();
            var visited = new HashSet<T>();

            foreach (var item in source)
            {
                Visit(item, visited, sorted, dependencies, throwOnCycle);
            }

            return sorted;
        }

        private static void Visit<T>(
            T item, HashSet<T> visited, List<T> sorted, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle)
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
    }
}
