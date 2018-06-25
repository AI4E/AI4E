using System;
using System.Collections.Immutable;

namespace AI4E.Internal
{
    internal static class ImmutableArrayExtension
    {
        public static T? FirstOrDefaultEx<T>(this ImmutableArray<T> collection, Func<T, bool> predicate)
           where T : struct
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            for (var i = 0; i < collection.Length; i++)
            {
                var t = collection[i];

                if (predicate(t))
                    return t;
            }

            return default;
        }

        public static int FindIndex<T>(this ImmutableArray<T> collection, Func<T, bool> predicate)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            for (var i = 0; i < collection.Length; i++)
            {
                if (predicate(collection[i]))
                    return i;
            }

            return -1;
        }
    }
}
