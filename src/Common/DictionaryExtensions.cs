using System;
using System.Collections.Generic;

namespace AI4E.Internal
{
    public static class DictionaryExtension
    {
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
