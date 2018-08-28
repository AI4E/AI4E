using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AI4E
{
    internal static class ConcurrentDictionaryExtension
    {
        [Obsolete("Use DictionaryExtension.Remove(IDictionary<TKey, TValue>, TKey, TValue)")]
        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue comparison)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));

            return ((IDictionary<TKey, TValue>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, comparison));
        }
    }
}
