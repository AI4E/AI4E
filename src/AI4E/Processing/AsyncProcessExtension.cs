using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E.Processing
{
    public static class AsyncProcessExtension
    {
        public static Task StartAsync(this IAsyncProcess asyncProcess, CancellationToken cancellation)
        {
            if (asyncProcess == null)
                throw new ArgumentNullException(nameof(asyncProcess));

            return asyncProcess.StartAsync().WithCancellation(cancellation);
        }

        public static Task TerminateAsync(this IAsyncProcess asyncProcess, CancellationToken cancellation)
        {
            if (asyncProcess == null)
                throw new ArgumentNullException(nameof(asyncProcess));

            return asyncProcess.TerminateAsync().WithCancellation(cancellation);
        }
    }
}
