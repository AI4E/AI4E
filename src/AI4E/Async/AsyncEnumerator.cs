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
 * AsyncEnumerator (https://github.com/Andrew-Hanlon/AsyncEnumerator)
 * MIT License
 * 
 * Copyright (c) 2017 Andrew Hanlon
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Async
{
    public interface IAsyncEnumeratorProducer<T>
    {
        T Break();
        Task Pause();
        Task Return(T value);
    }

    [AsyncMethodBuilder(typeof(AsyncEnumeratorMethodBuilder<>))]
    public sealed class AsyncEnumerator<T> : TaskLikeBase, IAsyncEnumeratorProducer<T>, IAsyncEnumerator<T>
    {
        private ExceptionDispatchInfo _exception;

        private bool _isStarted;
        private TaskCompletionSource<bool> _nextSource = new TaskCompletionSource<bool>();
        private TaskCompletionSource<bool> _yieldSource;

        public static TaskProvider<IAsyncEnumeratorProducer<T>> Capture() => TaskProvider<IAsyncEnumeratorProducer<T>>._instance;

        public T Current { get; internal set; }

        public Task<bool> MoveNext(CancellationToken cancellationToken = default)
        {
            _exception?.Throw();

            if (!_isStarted)
            {
                _isStarted = true;
                return _nextSource.Task;
            }

            _nextSource = new TaskCompletionSource<bool>();
            _yieldSource?.TrySetResult(true);

            return _yieldSource is null ? Task.FromResult(true) : _nextSource.Task;
        }

        internal override void SetException(ExceptionDispatchInfo exception)
        {
            _exception = exception;
            _nextSource.TrySetException(exception.SourceException);
        }

        T IAsyncEnumeratorProducer<T>.Break()
        {
            IsCompleted = true;
            _nextSource.TrySetResult(false);
            return default;
        }

        Task IAsyncEnumeratorProducer<T>.Pause()
        {
            _isStarted = true;
            _yieldSource = new TaskCompletionSource<bool>();
            return _yieldSource.Task;
        }

        Task IAsyncEnumeratorProducer<T>.Return(T value)
        {
            Current = value;
            _yieldSource = new TaskCompletionSource<bool>();
            _nextSource.TrySetResult(true);

            return _yieldSource.Task;
        }

        public void Dispose() { }
    }

    public class AsyncEnumeratorMethodBuilder<T>
    {
        private AsyncTaskMethodBuilder _methodBuilder;

        public static AsyncEnumeratorMethodBuilder<T> Create() => new AsyncEnumeratorMethodBuilder<T>(new AsyncEnumerator<T>());

        internal AsyncEnumeratorMethodBuilder(AsyncEnumerator<T> task)
        {
            _methodBuilder = AsyncTaskMethodBuilder.Create();
            Task = task;
        }

        public AsyncEnumerator<T> Task { get; }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            if (awaiter is TaskProvider<IAsyncEnumeratorProducer<T>>.TaskProviderAwaiter provider)
            {
                provider.OnCompleted(((IAsyncStateMachine)stateMachine).MoveNext, Task);
            }
            else
            {
                _methodBuilder.AwaitOnCompleted(ref awaiter, ref stateMachine);
            }
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            _methodBuilder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
        }

        public void SetException(Exception ex) => Task.SetException(ExceptionDispatchInfo.Capture(ex));

        public void SetResult(T result) => Task.SetCompletion();

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();
    }
}