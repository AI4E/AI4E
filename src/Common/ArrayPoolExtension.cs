using System;
using System.Buffers;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.SignalR.Client
{
    internal static class ArrayPoolExtension
    {
        public static ArrayPoolReleaser<T> Rent<T>(this ArrayPool<T> arrayPool, int length, out Memory<T> memory)
        {
            if (arrayPool == null)
                throw new ArgumentNullException(nameof(arrayPool));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0)
            {
                memory = Memory<T>.Empty;
                return default;
            }

            var array = arrayPool.Rent(length);
            try
            {
                memory = array.AsMemory().Slice(start: 0, length);
                return new ArrayPoolReleaser<T>(arrayPool, array);
            }
            catch
            {
                arrayPool.Return(array);
                throw;
            }
        }

        public readonly struct ArrayPoolReleaser<T> : IDisposable
        {
            private readonly ArrayPool<T> _arrayPool;
            private readonly T[] _array;

            internal ArrayPoolReleaser(ArrayPool<T> arrayPool, T[] array)
            {
                Assert(arrayPool != null);
                Assert(arrayPool != null);

                _arrayPool = arrayPool;
                _array = array;
            }

            public void Dispose()
            {
                _arrayPool?.Return(_array);
            }
        }
    }
}
