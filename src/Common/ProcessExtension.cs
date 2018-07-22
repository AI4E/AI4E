using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Internal
{
    public static class ProcessExtension
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellation = default)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            var tcs = new TaskCompletionSource<object>();

            process.EnableRaisingEvents = true;
            process.Exited += (s, o) => tcs.TrySetResult(null);

            // This is needed in order to prevent a race condition when the process exits before we can setup our event handler.
            process.Refresh();
            if (process.HasExited)
            {
                tcs.TrySetResult(null);
            }

            if (cancellation.CanBeCanceled)
            {
                cancellation.Register(() => tcs.TrySetCanceled());
            }

            return tcs.Task;
        }
    }
}
