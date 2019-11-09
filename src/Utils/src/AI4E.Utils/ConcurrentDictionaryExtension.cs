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

using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    public static class ConcurrentDictionaryExtension
    {
        public static bool TryRemove<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue comparison)
            where TKey : notnull
        {
#pragma warning disable CA1062
            return (dictionary as IDictionary<TKey, TValue>).Remove(new KeyValuePair<TKey, TValue>(key, comparison));
#pragma warning restore CA1062
        }
    }
}
