using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace AI4E.Async
{
    public sealed class AsyncDisposeHelper2 : IAsyncDisposable
    {
        #region Fields

        private readonly Func<ValueTask> _disposal;
        private volatile CancellationTokenSource _disposalCancellationSource = new CancellationTokenSource();
        private Task _disposalTask;

        // This is needed only if we have (or could have) an async dispose operation
        // -- OR --
        // we request the completion task explicitly.
        private TaskCompletionSource<object> _disposalTaskSource;

        // This is null if the disposal operation shall not be synced with the pending operations.
        private readonly AsyncReaderWriterLock _lock;

        #endregion

        #region C'tor

        public AsyncDisposeHelper2(Func<Task> disposal, AsyncDisposeHelperOptions options = default)
        {
            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            if (!options.IsValid())
                throw new ArgumentException("Invalid enum value.", nameof(options));

            _disposal = BuildDisposal(disposal);
            Options = options;

            _disposalTaskSource = new TaskCompletionSource<object>();

            if (options.IncludesFlag(AsyncDisposeHelperOptions.Synchronize))
            {
                _lock = new AsyncReaderWriterLock();
            }
        }

        public AsyncDisposeHelper2(Func<ValueTask> disposal, AsyncDisposeHelperOptions options = default)
        {
            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            if (!options.IsValid())
                throw new ArgumentException("Invalid enum value.", nameof(options));

            _disposal = disposal;
            Options = options;

            _disposalTaskSource = new TaskCompletionSource<object>();

            if (options.IncludesFlag(AsyncDisposeHelperOptions.Synchronize))
            {
                _lock = new AsyncReaderWriterLock();
            }
        }

        public AsyncDisposeHelper2(Action disposal, AsyncDisposeHelperOptions options = default)
        {
            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            if (!options.IsValid())
                throw new ArgumentException("Invalid enum value.", nameof(options));

            _disposal = BuildDisposal(disposal);
            Options = options;

            if (options.IncludesFlag(AsyncDisposeHelperOptions.Synchronize))
            {
                _lock = new AsyncReaderWriterLock();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            var disposalCancellationSource = Interlocked.Exchange(ref _disposalCancellationSource, null);
            if (disposalCancellationSource != null)
            {
                disposalCancellationSource.Cancel();

                Debug.Assert(_disposalTask == null);
                _disposalTask = DisposeInternalAsync();

                disposalCancellationSource.Dispose();
            }
        }

        public Task Disposal => GetOrCreateDisposalTaskSource().Task;

        public Task DisposeAsync()
        {
            Dispose();
            return Disposal;
        }

        #endregion

        public AsyncDisposeHelperOptions Options { get; }

        public DisposalGuard GuardDisposal(CancellationToken cancellation = default)
        {
            return new DisposalGuard(this, cancellation);
        }

        public ValueTask<DisposalGuard> GuardDisposalAsync(CancellationToken cancellation = default)
        {
            return DisposalGuard.CreateAsync(this, cancellation);
        }


        private static Func<ValueTask> BuildDisposal(Func<Task> disposal)
        {
            return () => new ValueTask(disposal());
        }

        private static Func<ValueTask> BuildDisposal(Action disposal)
        {
            return () =>
            {
                disposal();
                return new ValueTask(Task.CompletedTask);
            };
        }

        private static TaskCompletionSource<object> CompletedTaskCompletionSource { get; } = CreateCompletedTaskCompletionSource();

        private static TaskCompletionSource<object> CreateCompletedTaskCompletionSource()
        {
            var result = new TaskCompletionSource<object>();
            result.SetResult(null);
            return result;
        }


        private TaskCompletionSource<object> GetOrCreateDisposalTaskSource()
        {
            return GetOrCreateDisposalTaskSource(() => new TaskCompletionSource<object>());
        }

        private TaskCompletionSource<object> GetOrCreateDisposalTaskSource(Func<TaskCompletionSource<object>> factory)
        {
            var disposalTaskSource = _disposalTaskSource; // Volatile read op.

            if (disposalTaskSource == null)
            {
                disposalTaskSource = factory();

                var current = Interlocked.CompareExchange(ref _disposalTaskSource, disposalTaskSource, null);

                if (current != null)
                {
                    disposalTaskSource = current;
                }
            }

            return disposalTaskSource;
        }

        private async Task DisposeInternalAsync()
        {
            try
            {
                if (_lock != null)
                {
                    using (await _lock.WriterLockAsync())
                    {
                        await _disposal();
                    }
                }
                else
                {
                    await _disposal();
                }
            }
            catch (Exception exc) when (!(exc is OperationCanceledException))
            {
                // If the operation throws an exception we need to allocate a task completion source to allow for passing the excpetion to the outside.
                // TODO: Can we prevent the allocation by setting the excpetion to a dedicated field?
                GetOrCreateDisposalTaskSource().TrySetException(exc);
                return;
            }

            // The _disposalTaskSource field must not be null after the operation,
            // as this would lead to a lost wakeup, when the Disposal task is retrieved afterwards.
            // This is optimized by setting a singleton instance if there is no instance present yet.
            GetOrCreateDisposalTaskSource(() => CompletedTaskCompletionSource).TrySetResult(null);
        }

        public readonly struct DisposalGuard : IDisposable
        {
            private readonly CancellationTokenSource _combinedCancellationSource;
            private readonly IDisposable _lockReleaser;
            private readonly CancellationToken _externalCancellation;
            private readonly CancellationToken _disposal;

            public DisposalGuard(AsyncDisposeHelper2 asyncDisposeHelper, CancellationToken cancellation = default)
            {
                if (asyncDisposeHelper == null)
                    throw new ArgumentNullException(nameof(asyncDisposeHelper));

                GetBaseParameters(
                    asyncDisposeHelper,
                    cancellation,
                    out _combinedCancellationSource,
                    out _disposal,
                    out _externalCancellation);

                _lockReleaser = null;

                if (asyncDisposeHelper._lock != null)
                {
                    asyncDisposeHelper._lock.ReaderLock(cancellation);
                }
            }

            private DisposalGuard(CancellationTokenSource combinedCancellationSource,
                                  IDisposable lockReleaser,
                                  CancellationToken disposal,
                                  CancellationToken externalCancellation)
            {
                _combinedCancellationSource = combinedCancellationSource;
                _lockReleaser = lockReleaser;
                _disposal = disposal;
                _externalCancellation = externalCancellation;
            }

            internal static async ValueTask<DisposalGuard> CreateAsync(AsyncDisposeHelper2 asyncDisposeHelper, CancellationToken cancellation)
            {
                GetBaseParameters(
                    asyncDisposeHelper,
                    cancellation,
                    out var combinedCancellationSource,
                    out var disposal,
                    out var externalCancellation);

                var lockReleaser = default(IDisposable);

                if (asyncDisposeHelper._lock != null)
                {
                    lockReleaser = await asyncDisposeHelper._lock.ReaderLockAsync(cancellation);
                }

                return new DisposalGuard(combinedCancellationSource, lockReleaser, disposal, externalCancellation);
            }

            private static void GetBaseParameters(
                AsyncDisposeHelper2 asyncDisposeHelper,
                CancellationToken cancellation,
                out CancellationTokenSource combinedCancellationSource,
                out CancellationToken disposal,
                out CancellationToken externalCancellation)
            {
                var disposalCancellationSource = asyncDisposeHelper._disposalCancellationSource; // Volatile read op

                if (disposalCancellationSource == null || disposalCancellationSource.IsCancellationRequested)
                    throw new OperationCanceledException();

                externalCancellation = cancellation;
                disposal = disposalCancellationSource.Token;
                combinedCancellationSource = default;

                if (cancellation.CanBeCanceled)
                {
                    combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, disposal);
                }
            }

            public CancellationToken ExternalCancellation => _externalCancellation;
            public CancellationToken Cancellation => _combinedCancellationSource?.Token ?? Disposal;
            public CancellationToken Disposal => _disposal;

            public void Dispose()
            {
                _lockReleaser?.Dispose();
                _combinedCancellationSource?.Dispose();
            }
        }
    }

    [Flags]
    public enum AsyncDisposeHelperOptions
    {
        Default = 0,
        Synchronize = 1
    }

    public static class AsyncDisposeHelperExtension
    {
        public static void GuardDisposal(
            this AsyncDisposeHelper2 asyncDisposeHelper,
            Action<AsyncDisposeHelper2.DisposalGuard> action,
            CancellationToken cancellation = default)
        {
            if (asyncDisposeHelper == null)
                throw new ArgumentNullException(nameof(asyncDisposeHelper));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            using (var guard = asyncDisposeHelper.GuardDisposal(cancellation))
            {
                action(guard);
            }
        }

        public static async ValueTask GuardDisposalAsync(
            this AsyncDisposeHelper2 asyncDisposeHelper,
            Func<AsyncDisposeHelper2.DisposalGuard, Task> func,
            CancellationToken cancellation = default)
        {
            if (asyncDisposeHelper == null)
                throw new ArgumentNullException(nameof(asyncDisposeHelper));

            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using (var guard = await asyncDisposeHelper.GuardDisposalAsync(cancellation))
            {
                await func(guard);
            }
        }

        public static async ValueTask GuardDisposalAsync(
            this AsyncDisposeHelper2 asyncDisposeHelper,
            Func<AsyncDisposeHelper2.DisposalGuard, ValueTask> func,
            CancellationToken cancellation = default)
        {
            if (asyncDisposeHelper == null)
                throw new ArgumentNullException(nameof(asyncDisposeHelper));

            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using (var guard = await asyncDisposeHelper.GuardDisposalAsync(cancellation))
            {
                await func(guard);
            }
        }
    }
}
