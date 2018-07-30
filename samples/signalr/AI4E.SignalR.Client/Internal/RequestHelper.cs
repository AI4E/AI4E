using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Internal
{
    public static class RequestHelper
    {
        public static async Task<TResult> PerformRequestAsync<TResult>(Func<int, Task> requestOperation,
                                                                       Func<int, Task> cancelOperation,
                                                                       Func<int, TaskCompletionSource<TResult>, IDisposable> responseRegistration,
                                                                       Func<int> seqNumGenerator,
                                                                       CancellationToken cancellation)
        {
            if (requestOperation == null)
                throw new ArgumentNullException(nameof(requestOperation));

            if (cancelOperation == null)
                throw new ArgumentNullException(nameof(cancelOperation));

            if (responseRegistration == null)
                throw new ArgumentNullException(nameof(responseRegistration));

            if (seqNumGenerator == null)
                throw new ArgumentNullException(nameof(seqNumGenerator));

            var seqNum = seqNumGenerator();
            var responseSource = new TaskCompletionSource<TResult>();

            using (responseRegistration(seqNum, responseSource))
            {
                if (!cancellation.CanBeCanceled)
                {
                    return await InternalPerformRequestAsync(requestOperation, seqNum, responseSource);
                }

                using (cancellation.Register(() => CancelRequest(cancelOperation, seqNum, responseSource)))
                {
                    return await InternalPerformRequestAsync(requestOperation, seqNum, responseSource);
                }
            }
        }

        private static async void CancelRequest<TResult>(Func<int, Task> cancelOperation,
                                                   int seqNum,
                                                   TaskCompletionSource<TResult> responseSource)
        {
            try
            {
                var response = responseSource.Task.IgnoreCancellationAsync();
                var cancelRequest = RetryAsync(cancelOperation, seqNum, response);
                await Task.WhenAll(cancelRequest, response);
            }
            catch (Exception exc)
            {
                responseSource.TrySetException(exc);
            }
        }

        private static async Task<TResult> InternalPerformRequestAsync<TResult>(Func<int, Task> requestOperation,
                                                                                int seqNum,
                                                                                TaskCompletionSource<TResult> responseSource)
        {
            var response = responseSource.Task;
            var request = RetryAsync(requestOperation, seqNum, response.IgnoreCancellationAsync());
            await Task.WhenAll(request, response);
            return await response;
        }

        private static async Task RetryAsync(Func<int, Task> operation, int seqNum, Task response)
        {
            var delay = new TimeSpan(200 * TimeSpan.TicksPerMillisecond);
            var delayMax = new TimeSpan(12000 * TimeSpan.TicksPerMillisecond);
            Task completed = null;
            var delayCancellationSource = new CancellationTokenSource();

            do
            {
                await operation(seqNum);

                try
                {
                    completed = await Task.WhenAny(response, Task.Delay(delay, delayCancellationSource.Token));
                }
                finally
                {
                    if (completed == response)
                    {
                        delayCancellationSource.Cancel();
                    }
                }

                if (delay < delayMax)
                    delay = new TimeSpan(delay.Ticks * 2);
            }
            while (completed != response);
        }
    }
}
