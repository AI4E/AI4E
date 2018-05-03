using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

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

        internal AsyncInitializationHelper(Task initialization, CancellationTokenSource cancellation)
        {
            _initialization = initialization;
            _cancellation = cancellation;
        }

        public Task Initialization => _initialization ?? Task.CompletedTask;

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        public async Task<bool> CancelAsync()
        {
            Cancel();

            try
            {
                await Initialization;
                return true;
            }
            catch { }

            return false;
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

            this = default;

            _cancellation = new CancellationTokenSource();
            _initialization = InitInternalAsync(initialization);
        }

        public AsyncInitializationHelper(Func<Task<T>> initialization)
        {
            if (initialization == null)
                throw new ArgumentNullException(nameof(initialization));

            this = default;

            _cancellation = null;
            _initialization = InitInternalAsync(initialization); ;
        }

        public Task<T> Initialization => _initialization ?? Task.FromResult<T>(default);

        private async Task<T> InitInternalAsync(Func<CancellationToken, Task<T>> initialization)
        {
            Assert(initialization != null);

            await Task.Yield();

            return await initialization(_cancellation.Token);
        }

        private async Task<T> InitInternalAsync(Func<Task<T>> initialization)
        {
            Assert(initialization != null);

            await Task.Yield();

            return await initialization();
        }

        Task IAsyncInitialization.Initialization => Initialization;

        public void Cancel()
        {
            _cancellation?.Cancel();
        }

        public async Task<(bool success, T result)> CancelAsync()
        {
            Cancel();

            try
            {
                var result = await Initialization;
                return (true, result);
            }
            catch { }

            return (false, default);
        }

        public static implicit operator AsyncInitializationHelper(AsyncInitializationHelper<T> source)
        {
            return new AsyncInitializationHelper(source._initialization, source._cancellation);
        }
    }
}
