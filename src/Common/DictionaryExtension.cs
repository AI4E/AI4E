﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AI4E.Internal
{
    internal static class DictionaryExtension
    {
        // https://blogs.msdn.microsoft.com/pfxteam/2011/04/02/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
        public static bool Remove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue comparison)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));

            return dictionary.Remove(new KeyValuePair<TKey, TValue>(key, comparison));
        }

        public static bool Remove<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            if (dictionary is ConcurrentDictionary<TKey, TValue> concurrentDictionary)
                return concurrentDictionary.TryRemove(key, out value);

            if (dictionary.ContainsKey(key))
            {
                value = dictionary[key];
                dictionary.Remove(key);
                return true;
            }

            value = default;
            return false;
        }

#if NETSTANDARD
        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));

            if (dictionary.ContainsKey(key))
            {
                return false;
            }

            dictionary.Add(key, value);
            return true;
        }
#endif
    }
}
