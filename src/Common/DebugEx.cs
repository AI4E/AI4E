using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AI4E.Internal
{
    internal static class DebugEx
    {
        // condition is only checked if precondition mets.
        public static void Assert(bool precondition, bool condition)
        {
            // precondition => condition
            Debug.Assert(!precondition || condition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncEnumerable<T> AssertEach<T>(this IAsyncEnumerable<T> asyncEnumerable, Func<T, bool> assertion)
        {
#if !DEBUG
            return asyncEnumerable;
#endif

            return asyncEnumerable.Select(p =>
            {
                Debug.Assert(assertion(p));

                return p;
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncEnumerable<T> AssertEach<T>(this IAsyncEnumerable<T> asyncEnumerable, Func<T, bool> precondition, Func<T, bool> assertion)
        {
#if !DEBUG
            return asyncEnumerable;
#endif

            return asyncEnumerable.Select(p =>
            {
                if (precondition(p))
                {
                    Debug.Assert(assertion(p));
                }

                return p;
            });
        }
    }
}
