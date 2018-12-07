/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * Nito.AsyncEx
 * The MIT License (MIT)
 * 
 * Copyright (c) 2014 StephenCleary
 * 
 * All rights reserved.
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Based on: https://github.com/StephenCleary/AsyncEx/blob/master/src/Nito.AsyncEx.Coordination/AsyncLazy.cs

namespace AI4E.Async
{
    [Flags]
    public enum DisposableAsyncLazyOptions
    {
        None = 0,
        ExecuteOnCallingThread = 1,
        RetryOnFailure = 2,
        Autostart = 4
    }

    public sealed class DisposableAsyncLazy<T> : IAsyncDisposable
    {
        private static readonly Func<T, Task> _noDisposal = _ => System.Threading.Tasks.Task.CompletedTask;

        private readonly Func<Task<T>> _factory;
        private readonly Func<T, Task> _disposal;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly TaskCompletionSource<object> _disposalSource = new TaskCompletionSource<object>();
        private readonly object _lock = new object();

        private Lazy<Task<T>> _instance;
        private Task _disposeTask;

        #region C'tor

        public DisposableAsyncLazy(Func<Task<T>> factory, Func<T, Task> disposal, DisposableAsyncLazyOptions options = default)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            _cancellationSource = new CancellationTokenSource();
            _disposal = disposal;
            _factory = factory;

            if ((options & DisposableAsyncLazyOptions.RetryOnFailure) == DisposableAsyncLazyOptions.RetryOnFailure)
                _factory = RetryOnFailure(_factory);

            if ((options & DisposableAsyncLazyOptions.ExecuteOnCallingThread) != DisposableAsyncLazyOptions.ExecuteOnCallingThread)
                _factory = RunOnThreadPool(_factory);

            _instance = new Lazy<Task<T>>(_factory);

            if ((options & DisposableAsyncLazyOptions.Autostart) != DisposableAsyncLazyOptions.Autostart)
                Start();
        }

        public DisposableAsyncLazy(Func<CancellationToken, Task<T>> factory, Func<T, Task> disposal, DisposableAsyncLazyOptions options = default)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            if (disposal == null)
                throw new ArgumentNullException(nameof(disposal));

            _cancellationSource = new CancellationTokenSource();

            async Task<T> WithCancellation()
            {
                try
                {
                    return await factory(_cancellationSource.Token);
                }
                catch (OperationCanceledException) when (_cancellationSource.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }
            }

            _disposal = disposal;
            _factory = WithCancellation;

            if ((options & DisposableAsyncLazyOptions.RetryOnFailure) == DisposableAsyncLazyOptions.RetryOnFailure)
                _factory = RetryOnFailure(_factory);

            if ((options & DisposableAsyncLazyOptions.ExecuteOnCallingThread) != DisposableAsyncLazyOptions.ExecuteOnCallingThread)
                _factory = RunOnThreadPool(_factory);

            _instance = new Lazy<Task<T>>(_factory);

            if ((options & DisposableAsyncLazyOptions.Autostart) == DisposableAsyncLazyOptions.Autostart)
                Start();
        }

        public DisposableAsyncLazy(Func<Task<T>> factory, DisposableAsyncLazyOptions options = default)
            : this(factory, _noDisposal, options)
        { }

        public DisposableAsyncLazy(Func<CancellationToken, Task<T>> factory, DisposableAsyncLazyOptions options = default)
            : this(factory, _noDisposal, options)
        { }

        #endregion

        private Func<Task<T>> RetryOnFailure(Func<Task<T>> factory)
        {
            return async () =>
            {
                try
                {
                    return await factory().ConfigureAwait(false);
                }
                catch
                {
                    var cancellationRequested = true;

                    try
                    {
                        cancellationRequested = _cancellationSource.Token.IsCancellationRequested;
                    }
                    catch (ObjectDisposedException) { }

                    if (!cancellationRequested)
                    {
                        lock (_lock)
                        {
                            _instance = new Lazy<Task<T>>(_factory);
                        }
                    }
                    throw;
                }
            };
        }

        private Func<Task<T>> RunOnThreadPool(Func<Task<T>> factory)
        {
            return () => System.Threading.Tasks.Task.Run(factory);
        }

        /// <summary>
        /// Whether the asynchronous factory method has started. This is initially <c>false</c> and becomes <c>true</c> when this instance is awaited or after <see cref="Start"/> is called.
        /// </summary>
        public bool IsStarted
        {
            get
            {
                if (_cancellationSource.Token.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_lock)
                {
                    return _instance.IsValueCreated;
                }
            }
        }

        private bool IsNotYetStartedOrDisposed
        {
            get
            {
                if (_cancellationSource.Token.IsCancellationRequested)
                {
                    return true;
                }

                lock (_lock)
                {
                    return _instance.IsValueCreated;
                }
            }
        }

        /// <summary>
        /// Starts the asynchronous factory method, if it has not already started, and returns the resulting task.
        /// </summary>
        public Task<T> Task
        {
            get
            {
                if (_cancellationSource.Token.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                lock (_lock)
                {
                    return _instance.Value;
                }
            }
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy&lt;T&gt;"/> to be await'ed.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public TaskAwaiter<T> GetAwaiter()
        {
            return Task.GetAwaiter();
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy&lt;T&gt;"/> to be await'ed.
        /// </summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return Task.ConfigureAwait(continueOnCapturedContext);
        }

        /// <summary>
        /// Starts the asynchronous initialization, if it has not already started.
        /// </summary>
        public void Start()
        {
            var unused = Task;
        }

        public Task Disposal => _disposalSource.Task;

        public void Dispose()
        {
            lock (_disposalSource)
            {
                if (_disposeTask == null)
                {
                    _cancellationSource.Cancel();
                    _disposeTask = DisposeInternalAsync();
                    _cancellationSource.Dispose();
                }
            }
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Disposal;
        }

        private async Task DisposeInternalAsync()
        {
            try
            {
                if (!IsNotYetStartedOrDisposed)
                {
                    T result;
                    try
                    {
                        result = await Task;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    await _disposal(result);
                }
            }
            catch (Exception exc)
            {
                _disposalSource.TrySetException(exc);
            }
            finally
            {
                _disposalSource.SetResult(null);
            }
        }
    }
}
