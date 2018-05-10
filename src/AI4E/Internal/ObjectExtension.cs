using System;
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E.Internal
{
    internal static class ObjectExtension
    {
        public static void DisposeIfDisposable(this object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public static Task DisposeIfDisposableAsync(this object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            if (obj is IAsyncDisposable asyncDisposable)
            {
                return asyncDisposable.DisposeAsync();
            }

            if (obj is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return Task.CompletedTask;
        }
    }
}
