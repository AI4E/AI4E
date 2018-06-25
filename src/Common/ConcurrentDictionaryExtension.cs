using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AI4E.Internal
{
    internal static class ConcurrentDictionaryExtension
    {
        // https://blogs.msdn.microsoft.com/pfxteam/2011/04/02/little-known-gems-atomic-conditional-removals-from-concurrentdictionary/
        public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue comparison)
        {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));

            return ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, comparison));
        }
    }
}
