using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Internal
{
    internal sealed class TaskCancellationTokenSource : IDisposable
    {
        private static readonly CancellationToken _cancelledCancellationToken;

        static TaskCancellationTokenSource()
        {
            var tcs = new CancellationTokenSource();
            tcs.Cancel();
            _cancelledCancellationToken = tcs.Token;
        }

        private volatile CancellationTokenSource _cancellationTokenSource;

        public TaskCancellationTokenSource(Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            Task = task;

            if (!task.IsCompleted)
            {
                _cancellationTokenSource = new CancellationTokenSource();

                task.GetAwaiter().OnCompleted(() =>
                {
                    _cancellationTokenSource?.Cancel(); // Volatile read op.
                });
            }
        }

        public CancellationToken Token => _cancellationTokenSource?.Token ?? // Volatile read op.
                                          _cancelledCancellationToken;

        public Task Task { get; }

        public void Dispose()
        {
            var cancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, null);
            cancellationTokenSource?.Dispose();
        }
    }
}
