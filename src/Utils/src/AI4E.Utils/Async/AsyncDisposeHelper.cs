/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace AI4E.Utils.Async
{
    /// <summary>
    /// A helper that can be used to safely dispose of objects in a thread-safe way.
    /// </summary>
    public sealed class AsyncDisposeHelper : IAsyncDisposable, IDisposable
    {
        #region Fields

        private readonly Func<ValueTask> _disposal;
#pragma warning disable CA2213
        internal volatile CancellationTokenSource? _disposalCancellationSource = new CancellationTokenSource();
#pragma warning restore CA2213
        private Task? _disposalTask;

        // This is needed only if we have (or could have) an async dispose operation
        // -- OR --
        // we request the completion task explicitly.
        private TaskCompletionSource<object?>? _disposalTaskSource;
        private volatile Exception? _exception;

        // This is null if the disposal operation shall not be synced with the pending operations.
        internal readonly AsyncReaderWriterLock? _lock;

        // This is null if recursion detection is disabled.
        private readonly AsyncLocal<bool>? _recursionDetection;

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncDisposeHelper"/> type.
        /// </summary>
        /// <param name="disposal">The dispose operation that shall be invoked on dispose.</param>
        /// <param name="options">A combination of options that specify the behavior.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="disposal"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="options"/> is not a valid combination of flags as specified in <see cref="AsyncDisposeHelperOptions"/>.
        /// </exception>
        public AsyncDisposeHelper(Func<Task> disposal, AsyncDisposeHelperOptions options = default)
        {
            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            if (!options.IsValid())
                throw new ArgumentException("Invalid enum value.", nameof(options));

            _disposal = BuildDisposal(disposal);
            Options = options;

            _disposalTaskSource = new TaskCompletionSource<object?>();

            if (options.IncludesFlag(AsyncDisposeHelperOptions.Synchronize))
            {
                _lock = new AsyncReaderWriterLock();
            }

            if (!options.IncludesFlag(AsyncDisposeHelperOptions.DisableRecursionDetection))
            {
                _recursionDetection = new AsyncLocal<bool>();
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncDisposeHelper"/> type.
        /// </summary>
        /// <param name="disposal">The dispose operation that shall be invoked on dispose.</param>
        /// <param name="options">A combination of options that specify the behavior.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="disposal"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="options"/> is not a valid combination of flags as specified in <see cref="AsyncDisposeHelperOptions"/>.
        /// </exception>
        public AsyncDisposeHelper(Func<ValueTask> disposal, AsyncDisposeHelperOptions options = default)
        {
            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            if (!options.IsValid())
                throw new ArgumentException("Invalid enum value.", nameof(options));

            _disposal = disposal;
            Options = options;

            _disposalTaskSource = new TaskCompletionSource<object?>();

            if (options.IncludesFlag(AsyncDisposeHelperOptions.Synchronize))
            {
                _lock = new AsyncReaderWriterLock();
            }

            if (!options.IncludesFlag(AsyncDisposeHelperOptions.DisableRecursionDetection))
            {
                _recursionDetection = new AsyncLocal<bool>();
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncDisposeHelper"/> type.
        /// </summary>
        /// <param name="disposal">The dispose operation that shall be invoked on dispose.</param>
        /// <param name="options">A combination of options that specify the behavior.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="disposal"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="options"/> is not a valid combination of flags as specified in <see cref="AsyncDisposeHelperOptions"/>.
        /// </exception>
        public AsyncDisposeHelper(Action disposal, AsyncDisposeHelperOptions options = default)
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

            if (!options.IncludesFlag(AsyncDisposeHelperOptions.DisableRecursionDetection))
            {
                _recursionDetection = new AsyncLocal<bool>();
            }
        }

        public AsyncDisposeHelper() : this(() => { }, AsyncDisposeHelperOptions.DisableRecursionDetection) { }

        #endregion

        #region IAsyncDisposable

        /// <summary>
        /// Starts the dispose of the object and does not wait for the end of the dispose operation.
        /// </summary>
        public void Dispose()
        {
            // Volatile read op.
            if (_disposalCancellationSource == null)
                return;

            var disposalCancellationSource = Interlocked.Exchange(ref _disposalCancellationSource, null);

            if (disposalCancellationSource != null)
            {
                disposalCancellationSource.Cancel();

                Debug.Assert(_disposalTask == null);
                _disposalTask = DisposeInternalAsync();

                disposalCancellationSource.Dispose();
            }
        }

        /// <summary>
        /// Gets a task that represents the asynchronous dispose operation.
        /// </summary>
        /// <remarks>
        /// The value cannot be retrieved in the dispose operation itself,
        /// as this would lead to deadlock situations if the returning task is awaited.
        /// Instead, a completed task is returned.
        /// This behavior can be changed by specifying the <see cref="AsyncDisposeHelperOptions.DisableRecursionDetection"/> option on creation.
        /// </remarks>
        public Task Disposal
        {
            get
            {
                // Recursion detection is enabled and we are trying to retrieve the Disposal in the _dispose operation.
                if (_recursionDetection != null && _recursionDetection.Value)
                {

                    // Fake the disposal in order to prevent deadlocks.
                    // This behavior can be changed by specifying the 'DisableRecursionDetection' option.
                    return Task.CompletedTask;
                }

                var disposalTaskSource = GetOrCreateDisposalTaskSource();

                if (disposalTaskSource == CompletedTaskCompletionSource)
                {
                    var exception = _exception; // Volatile read op.
                    if (exception != null)
                    {
                        // Re-throw the exception preserving stack-trace information.
                        ExceptionDispatchInfo.Capture(exception).Throw();
                    }
                }

                return disposalTaskSource.Task;
            }
        }

        /// <summary>
        /// Starts the dispose of the object and returns a task that represents the asynchronous dispose operation.
        /// </summary>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        /// <remarks>
        /// When this operation is called in the dispose operation itself,
        /// the returned task is always completed, to prevent deadlock situations if awaited.
        /// This behavior can be changed by specifying the <see cref="AsyncDisposeHelperOptions.DisableRecursionDetection"/> option on creation.
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            Dispose();

            return Disposal.AsValueTask();
        }

        #endregion

        /// <summary>
        /// Gets the options that were used to create this object.
        /// </summary>
        public AsyncDisposeHelperOptions Options { get; }

        /// <summary>
        /// Guards against disposal and returns a <see cref="DisposalGuard"/>.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> that is passed to the guard to get the combined cancellation.</param>
        /// <returns>A disposal guard.</returns>
        public DisposalGuard GuardDisposal(CancellationToken cancellation = default)
        {
            return new DisposalGuard(this, cancellation);
        }

        /// <summary>
        /// Asynchronously guards against disposal and returns a <see cref="DisposalGuard"/>.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> that is passed to the guard to get the combined cancellation.</param>
        /// <returns>A task representing the asynchronous operation. When evaluated, the tasks result contains the disposal guard.</returns>
        public ValueTask<DisposalGuard> GuardDisposalAsync(CancellationToken cancellation = default)
        {
            return DisposalGuard.CreateAsync(this, cancellation);
        }

        /// <summary>
        /// Gets a boolean value indicating whether the object is disposed.
        /// </summary>
        /// <remarks>
        /// Be aware that the returned value is just a snapshot in time and may already be invalid if returned.
        /// If this returns true, it is guaranteed that the value will never change again in the future.
        /// </remarks>
        public bool IsDisposed
        {
            get
            {
                // Volatile read op
                var disposalCancellationSource = _disposalCancellationSource;

                if (disposalCancellationSource == null)
                    return true;

                return disposalCancellationSource.IsCancellationRequested;
            }
        }

        public CancellationToken AsCancellationToken()
        {
            return _disposalCancellationSource?.Token ?? new CancellationToken(canceled: true);
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

        private static TaskCompletionSource<object?> CompletedTaskCompletionSource { get; } = CreateCompletedTaskCompletionSource();

        private static TaskCompletionSource<object?> CreateCompletedTaskCompletionSource()
        {
            var result = new TaskCompletionSource<object?>();
            result.SetResult(null);
            return result;
        }


        private TaskCompletionSource<object?> GetOrCreateDisposalTaskSource()
        {
            return GetOrCreateDisposalTaskSource(() => new TaskCompletionSource<object?>());
        }

        private TaskCompletionSource<object?> GetOrCreateDisposalTaskSource(Func<TaskCompletionSource<object?>> factory)
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
                        await DisposalWithRecursionDetection().ConfigureAwait(false);
                    }
                }
                else
                {
                    await DisposalWithRecursionDetection().ConfigureAwait(false);
                }
            }
            catch (Exception exc) when (!(exc is OperationCanceledException))
            {
                // If the operation throws an exception we need to allocate a task completion source to allow for passing the exception to the outside.
                // We prevent the allocation by setting the exception to a dedicated field.

                // The exception MUST be written volatile to prevent a situation that _exception is written after _disposalTaskSource.
                _exception = exc; // Volatile write op.
                GetOrCreateDisposalTaskSource(() => CompletedTaskCompletionSource).TrySetException(exc);
                return;
            }

            // The _disposalTaskSource field must not be null after the operation,
            // as this would lead to a lost wakeup, when the Disposal task is retrieved afterwards.
            // This is optimized by setting a singleton instance if there is no instance present yet.
            GetOrCreateDisposalTaskSource(() => CompletedTaskCompletionSource).TrySetResult(null);
        }

        private async Task DisposalWithRecursionDetection()
        {
            if (_recursionDetection != null)
            {
                _recursionDetection.Value = true;
                await _disposal();
                _recursionDetection.Value = false;
            }
            else
            {
                await _disposal();
            }
        }
    }

#pragma warning disable CA1815
    public readonly struct DisposalGuard : IDisposable
#pragma warning restore CA1815
    {
        private readonly CancellationTokenSource? _combinedCancellationSource;
        private readonly IDisposable? _lockReleaser;
        private readonly CancellationToken _externalCancellation;
        private readonly CancellationToken _disposal;

        internal DisposalGuard(AsyncDisposeHelper asyncDisposeHelper, CancellationToken cancellation = default)
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
                _lockReleaser = asyncDisposeHelper._lock.ReaderLock(cancellation);

                if ((_combinedCancellationSource?.Token ?? _disposal).IsCancellationRequested)
                {
                    _lockReleaser.Dispose();
                    _combinedCancellationSource?.Dispose();
                    throw new OperationCanceledException();
                }
            }
        }

        private DisposalGuard(CancellationTokenSource? combinedCancellationSource,
                              IDisposable? lockReleaser,
                              CancellationToken disposal,
                              CancellationToken externalCancellation)
        {
            _combinedCancellationSource = combinedCancellationSource;
            _lockReleaser = lockReleaser;
            _disposal = disposal;
            _externalCancellation = externalCancellation;
        }

        internal static async ValueTask<DisposalGuard> CreateAsync(AsyncDisposeHelper asyncDisposeHelper, CancellationToken cancellation)
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

                if ((combinedCancellationSource?.Token ?? disposal).IsCancellationRequested)
                {
                    lockReleaser.Dispose();
                    combinedCancellationSource?.Dispose();
                    throw new OperationCanceledException();
                }
            }

            return new DisposalGuard(combinedCancellationSource, lockReleaser, disposal, externalCancellation);
        }

        private static void GetBaseParameters(
            AsyncDisposeHelper asyncDisposeHelper,
            CancellationToken cancellation,
            out CancellationTokenSource? combinedCancellationSource,
            out CancellationToken disposal,
            out CancellationToken externalCancellation)
        {
            var disposalCancellationSource = asyncDisposeHelper._disposalCancellationSource; // Volatile read op

            if (cancellation.IsCancellationRequested ||
                disposalCancellationSource == null ||
                disposalCancellationSource.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

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

    [Flags]
    public enum AsyncDisposeHelperOptions
    {
        Default = 0,
        Synchronize = 1,
        DisableRecursionDetection = 2
    }

    public static class AsyncDisposeHelperExtension
    {
        public static void GuardDisposal(
            this AsyncDisposeHelper asyncDisposeHelper,
            Action<DisposalGuard> action,
            CancellationToken cancellation = default)
        {
            if (asyncDisposeHelper == null)
                throw new ArgumentNullException(nameof(asyncDisposeHelper));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            using var guard = asyncDisposeHelper.GuardDisposal(cancellation);
            action(guard);
        }

        public static async ValueTask GuardDisposalAsync(
            this AsyncDisposeHelper asyncDisposeHelper,
            Func<DisposalGuard, Task> func,
            CancellationToken cancellation = default)
        {
            if (asyncDisposeHelper == null)
                throw new ArgumentNullException(nameof(asyncDisposeHelper));

            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using var guard = await asyncDisposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
            await func(guard).ConfigureAwait(false);
        }

        public static async ValueTask GuardDisposalAsync(
            this AsyncDisposeHelper asyncDisposeHelper,
            Func<DisposalGuard, ValueTask> func,
            CancellationToken cancellation = default)
        {
            if (asyncDisposeHelper == null)
                throw new ArgumentNullException(nameof(asyncDisposeHelper));

            if (func == null)
                throw new ArgumentNullException(nameof(func));

            using var guard = await asyncDisposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
            await func(guard).ConfigureAwait(false);
        }
    }
}
