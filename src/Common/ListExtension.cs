using System;
using System.Collections.Generic;

namespace AI4E.Internal
{
    internal static class IListExtension
    {
        public static T RemoveFirstWhere<T>(this IList<T> list, Func<T, bool> predicate)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            for (var i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    var result = list[i];
                    list.RemoveAt(i);
                    return result;
                }
            }

            return default;
        }
    }
}
