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

namespace AI4E.Utils
{
    public static class AI4EUtilsCollectionExtension
    {
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            if (items is null)
                throw new ArgumentNullException(nameof(items));

            if (collection is List<T> list)
            {
                list.AddRange(items);
                return;
            }

            foreach (var item in items)
            {
#pragma warning disable CA1062
                collection.Add(item);
#pragma warning restore CA1062
            }
        }

        public static void Replace<T>(this ICollection<T> collection, T oldItem, T newItem)
        {
            if (collection is IList<T> list)
            {
                var index = list.IndexOf(oldItem);

                if (index < 0)
                    return;

                list.RemoveAt(index);
                list.Insert(index, newItem);
                return;
            }

#pragma warning disable CA1062
            if (collection.Remove(oldItem))
#pragma warning restore CA1062
            {
                collection.Add(newItem);
            }
        }
    }
}
