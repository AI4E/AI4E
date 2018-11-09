using System;
using System.Threading.Tasks;

namespace AI4E.Internal
{
    public static class TaskCompletionSourceExtension
    {
        public static bool TrySetExceptionOrCancelled<T>(this TaskCompletionSource<T> taskCompletionSource, Exception exception)
        {
            if (taskCompletionSource == null)
                throw new ArgumentNullException(nameof(taskCompletionSource));

            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            if (exception is OperationCanceledException operationCanceledException)
            {
                var cancellationToken = operationCanceledException.CancellationToken;

                if (cancellationToken != default)
                {
                    return taskCompletionSource.TrySetCanceled(cancellationToken);
                }

                return taskCompletionSource.TrySetCanceled();
            }

            return taskCompletionSource.TrySetException(exception);
        }
    }
}
