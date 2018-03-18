using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Async
{
    public readonly struct AsyncInitializationHelper : IAsyncInitialization
    {
        private readonly Task _initialization;
        private readonly CancellationTokenSource _cancellation;

        public AsyncInitializationHelper(Func<CancellationToken, Task> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            _cancellation = new CancellationTokenSource();
            _initialization = initialization(_cancellation.Token);
        }

        public AsyncInitializationHelper(Func<Task> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            _cancellation = null;
            _initialization = initialization();
        }

        public Task Initialization => _initialization ?? Task.CompletedTask;

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        public async Task CancelAsync()
        {
            Cancel();

            try
            {
                await Initialization;
            }
            catch (OperationCanceledException) { }
        }
    }

    public readonly struct AsyncInitializationHelper<T> : IAsyncInitialization
    {
        private readonly Task<T> _initialization;
        private readonly CancellationTokenSource _cancellation;

        public AsyncInitializationHelper(Func<CancellationToken, Task<T>> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            _cancellation = new CancellationTokenSource();
            _initialization = initialization(_cancellation.Token);
        }

        public AsyncInitializationHelper(Func<Task<T>> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            _cancellation = null;
            _initialization = initialization();
        }

        public Task<T> Initialization => _initialization ?? Task.FromResult<T>(default);

        Task IAsyncInitialization.Initialization => Initialization;

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        public async Task CancelAsync()
        {
            Cancel();

            try
            {
                await Initialization;
            }
            catch { }
        }
    }
}
