using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AI4E.Internal
{
    public static class DictionaryExtension
    {
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
    }
}
