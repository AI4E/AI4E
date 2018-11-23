using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;

namespace AI4E.Async
{
    public sealed class OneTimeOperation
    {
        private readonly Func<Task> _operation;
        private readonly TaskCompletionSource<object> _executionSource = new TaskCompletionSource<object>();
        private readonly object _lock = new object();

        private Task _executeTask;
        private volatile bool _hasStarted = false;

        public OneTimeOperation(Func<Task> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            _operation = operation;
        }

        public Task ExecuteAsync(CancellationToken cancellation)
        {
            return ExecuteAsync().WithCancellation(cancellation);
        }

        public Task ExecuteAsync()
        {
            Execute();
            return Execution;
        }

        public void Execute()
        {
            if (_hasStarted) // Volatile read op.
                return;

            lock (_executionSource)
            {
                // We use a dedicated flag for specifying whether the operation was already started 
                // instead of simply check _executeTask for beeing set already to allow 
                // recursive calls to Execute() in the executed operation.

                if (_hasStarted)
                    return;

                _hasStarted = true;

                Debug.Assert(_executeTask == null);

                _executeTask = ExecuteInternalAsync();
            }
        }

        private async Task ExecuteInternalAsync()
        {
#if DEBUG
            var executionSourceSetLocally = false;
#endif
            try
            {
                try
                {
                    await _operation();
                }
                catch (OperationCanceledException exc)
                {
                    bool successfullySetExecutionSource;

                    if (exc.CancellationToken == default)
                    {
                        successfullySetExecutionSource = _executionSource.TrySetCanceled();
                    }
                    else
                    {
                        successfullySetExecutionSource = _executionSource.TrySetCanceled(exc.CancellationToken);
                    }

#if DEBUG
                    Debug.Assert(successfullySetExecutionSource);
                    executionSourceSetLocally = true;
#endif
                }
                catch (Exception exc)
                {
                    var successfullySetExecutionSource = _executionSource.TrySetException(exc);

#if DEBUG
                    Debug.Assert(successfullySetExecutionSource);
                    executionSourceSetLocally = true;
#endif
                }
            }
            finally
            {
                var executionSourceSet = _executionSource.TrySetResult(null);
#if DEBUG
                Debug.Assert(executionSourceSet || executionSourceSetLocally);
#endif
            }
        }

        public Task Execution => _executionSource.Task;
    }
}
