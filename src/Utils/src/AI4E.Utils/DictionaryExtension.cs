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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace System.Collections.Generic
{
    public static class AI4EUtilsDictionaryExtension
    {
        // https://blogs.msdn.microsoft.com/pfxteam/2011/04/02/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
        public static bool Remove<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key, TValue comparison)
            where TKey : notnull
        {
#pragma warning disable CA1062
            return dictionary.Remove(new KeyValuePair<TKey, TValue>(key, comparison));
#pragma warning restore CA1062
        }

#if NETSTD20
        public static bool Remove<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key, [MaybeNullWhen(false)]out TValue value)
            where TKey : notnull
        {
            if (dictionary is ConcurrentDictionary<TKey, TValue> concurrentDictionary)
                return concurrentDictionary.TryRemove(key, out value);

#pragma warning disable CA1062
            if (dictionary.ContainsKey(key))
#pragma warning restore CA1062
            {
                value = dictionary[key];
                dictionary.Remove(key);
                return true;
            }

            value = default!;
            return false;
        }


        public static bool TryAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
            where TKey : notnull
        {
#pragma warning disable CA1062
            if (dictionary.ContainsKey(key))
#pragma warning restore CA1062
            {
                return false;
            }

            dictionary.Add(key, value);
            return true;
        }
#endif

        public static TValue GetOrAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
            where TKey : notnull
        {
#pragma warning disable CA1062
            if (dictionary.TryGetValue(key, out var result))
#pragma warning restore CA1062
                return result;

            dictionary.Add(key, value);
            return value;
        }

        public static TValue GetOrAdd<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory)
            where TKey : notnull
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

#pragma warning disable CA1062
            if (dictionary.TryGetValue(key, out var result))
#pragma warning restore CA1062
                return result;

            result = factory(key);
            dictionary.Add(key, result);
            return result;
        }

        public static bool AddOrReplace<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue value)
            where TKey : notnull
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

#pragma warning disable CA1062
            if (dictionary.TryGetValue(key, out var comparison)
#pragma warning restore CA1062
               && EqualityComparer<TValue>.Default.Equals(comparison, value))
            {
                return false;
            }

            dictionary[key] = value;
            return true;
        }
    }
}
