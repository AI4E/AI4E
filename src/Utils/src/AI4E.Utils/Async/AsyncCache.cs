/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Async
{
    /// <summary>
    /// Represents an updatable cache with asynchronous update operation.
    /// </summary>
    /// <typeparam name="T">The type of cached value.</typeparam>
    /// <remarks>
    /// Update operations may be called concurrently.
    /// This type is thread-safe.
    /// </remarks>
    public sealed class AsyncCache<T> : IDisposable
    {
        private readonly Func<CancellationToken, Task<T>> _operation;
        private TaskCompletionSource<T>? _taskCompletionSource;
        private Task? _currentUpdate;
        private CancellationTokenSource? _currentUpdateCancellationSource;
        private readonly object _mutex = new object();
        private CancellationTokenSource? _disposalCancellationSource;

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncCache{T}"/> type with the specified update operation.
        /// </summary>
        /// <param name="operation">The asynchronous update operation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="operation"/> is <c>null</c>.</exception>
        public AsyncCache(Func<CancellationToken, Task<T>> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            _operation = operation;
            _disposalCancellationSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncCache{T}"/> type with the specified update operation.
        /// </summary>
        /// <param name="operation">The asynchronous update operation.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="operation"/> is <c>null</c>.</exception>
        public AsyncCache(Func<Task<T>> operation) : this(_ => operation()) { }

        /// <summary>
        /// Gets a task thats result is the cached value.
        /// </summary>
        public Task<T> Task
        {
            get
            {
                CheckObjectDisposed();

                lock (_mutex)
                {
                    if (_taskCompletionSource == null)
                    {
                        DoUpdateInternal();
                    }

                    return _taskCompletionSource!.Task;
                }
            }
        }

        private void DoUpdateInternal()
        {
            var disposalCancellationSource = Volatile.Read(ref _disposalCancellationSource);

            if (disposalCancellationSource is null)
            {
                ThrowObjectDisposed();
            }

            if (_taskCompletionSource != null)
            {
                Debug.Assert(_currentUpdateCancellationSource != null);
                Debug.Assert(_currentUpdate != null);

                _currentUpdateCancellationSource!.Cancel();
                _currentUpdateCancellationSource.Dispose();
                _currentUpdate!.HandleExceptions();
            }

            if (_taskCompletionSource == null || _taskCompletionSource.Task.Status != TaskStatus.WaitingForActivation)
            {
                _taskCompletionSource = new TaskCompletionSource<T>();
            }

            _currentUpdateCancellationSource
                = CancellationTokenSource.CreateLinkedTokenSource(disposalCancellationSource!.Token);

            _currentUpdate = UpdateInternalAsync(
                _taskCompletionSource,
                _currentUpdateCancellationSource,
                disposalCancellationSource);
        }

        private async Task UpdateInternalAsync(
             TaskCompletionSource<T> taskCompletionSource,
            CancellationTokenSource cancellationTokenSource,
            CancellationTokenSource disposedCancellationSource)
        {
            var cancellation = cancellationTokenSource.Token;

            try
            {
                var result = await _operation(cancellation).ConfigureAwait(false);

                lock (_mutex)
                {
                    if (cancellationTokenSource == _currentUpdateCancellationSource)
                    {
                        if (disposedCancellationSource.IsCancellationRequested)
                        {
                            taskCompletionSource.SetException(new ObjectDisposedException(GetType().FullName));
                        }
                        else
                        {
                            taskCompletionSource.SetResult(result);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_mutex)
                {
                    if (cancellationTokenSource == _currentUpdateCancellationSource)
                    {
                        if (disposedCancellationSource.IsCancellationRequested)
                        {
                            taskCompletionSource.SetException(new ObjectDisposedException(GetType().FullName));
                        }
                        else
                        {
                            taskCompletionSource.SetCanceled();
                        }
                    }
                }
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                lock (_mutex)
                {
                    if (cancellationTokenSource == _currentUpdateCancellationSource)
                    {
                        taskCompletionSource.SetException(exc);
                    }
                }
            }
        }

        /// <summary>
        /// Initiated a cache update.
        /// </summary>
        public void Update()
        {
            CheckObjectDisposed();

            lock (_mutex)
            {
                DoUpdateInternal();
            }
        }

        /// <summary>
        /// Initiated a cache update and returns a task thats result contains the cached value.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asychronous operation
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="Task{TResult}"/> thats result contains the cached value.</returns>
        public Task<T> UpdateAsync(CancellationToken cancellation = default)
        {
            CheckObjectDisposed();

            Task<T> task;

            lock (_mutex)
            {
                DoUpdateInternal();
                task = _taskCompletionSource!.Task;
            }

            return task.WithCancellation(cancellation);
        }

        /// <summary>
        /// Disposed of the current instance.
        /// </summary>
        public void Dispose()
        {
            var disposedCancellationSource = Interlocked.Exchange(ref _disposalCancellationSource, null);
            disposedCancellationSource?.Cancel();
            disposedCancellationSource?.Dispose();

            lock (_mutex)
            {
                if (_taskCompletionSource != null)
                {
                    Debug.Assert(_currentUpdateCancellationSource != null);
                    Debug.Assert(_currentUpdate != null);

                    _currentUpdateCancellationSource!.Cancel();
                    _currentUpdateCancellationSource.Dispose();
                    _currentUpdate!.HandleExceptions();
                }
            }
        }

        /// <summary>
        /// Gets an awaiter used to await the instance.
        /// </summary>
        /// <returns>The task awaiter.</returns>
        public TaskAwaiter<T> GetAwaiter()
        {
            return Task.GetAwaiter();
        }

        private void CheckObjectDisposed(out CancellationTokenSource disposalCancellationSource)
        {
            disposalCancellationSource = Volatile.Read(ref _disposalCancellationSource)!;

            if (disposalCancellationSource == null || disposalCancellationSource.IsCancellationRequested)
                ThrowObjectDisposed();
        }

        private void CheckObjectDisposed()
        {
            CheckObjectDisposed(out _);
        }

        private void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
