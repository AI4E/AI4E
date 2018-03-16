using System;

namespace AI4E.Storage.InMemory.Internal
{
    // Based on: https://github.com/Burtsev-Alexey/net-object-deep-copy/
    internal static class ArrayExtensions
    {
        public static void ForEach(this Array array, Action<Array, int[]> action)
        {
            if (array.LongLength == 0)
                return;

            var walker = new ArrayTraverse(array);
            do
            {
                action(array, walker.Position);
            }
            while (walker.Step());
        }
    }
}
