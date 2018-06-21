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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Async
{
    public interface IAsyncSequenceProducer<T>
    {
        T Break();
        void Return(T value);
    }

    [AsyncMethodBuilder(typeof(AsyncSequenceMethodBuilder<>))]
    public sealed class AsyncSequence<T> : TaskLikeBase, IAsyncSequenceProducer<T>, IAsyncEnumerator<T>
    {
        private readonly ConcurrentQueue<T> _valueQueue = new ConcurrentQueue<T>();
        private ExceptionDispatchInfo _exception;

        private TaskCompletionSource<bool> _nextSource;

        public static TaskProvider<IAsyncSequenceProducer<T>> Capture() => TaskProvider<IAsyncSequenceProducer<T>>._instance;

        public T Current { get; private set; }

        public async Task<bool> MoveNext(CancellationToken cancellationToken = default)
        {
            _exception?.Throw();

            if (_valueQueue.TryDequeue(out var value))
            {
                Current = value;
                return true;
            }

            if (IsCompleted)
                return false;

            _nextSource = new TaskCompletionSource<bool>();

            if (cancellationToken.CanBeCanceled)
            {
                using (cancellationToken.Register(() => _nextSource.TrySetCanceled()))
                {
                    await _nextSource.Task;
                }
            }
            else
            {
                await _nextSource.Task;
            }

            if (!_valueQueue.TryDequeue(out value))
                return !IsCompleted;

            Current = value;
            return true;
        }

        internal override void SetException(ExceptionDispatchInfo exception)
        {
            _exception = exception;
            _nextSource?.TrySetException(exception.SourceException);
        }

        T IAsyncSequenceProducer<T>.Break()
        {
            IsCompleted = true;
            _nextSource?.TrySetResult(false);
            return default;
        }

        void IAsyncSequenceProducer<T>.Return(T value)
        {
            _valueQueue.Enqueue(value);
            _nextSource?.TrySetResult(true);
        }

        public void Dispose() { }
    }

    public readonly struct AsyncSequenceMethodBuilder<T>
    {
        private readonly AsyncTaskMethodBuilder<T> _methodBuilder;

        public static AsyncSequenceMethodBuilder<T> Create() => new AsyncSequenceMethodBuilder<T>(new AsyncSequence<T>());

        internal AsyncSequenceMethodBuilder(AsyncSequence<T> task)
        {
            _methodBuilder = AsyncTaskMethodBuilder<T>.Create();
            Task = task;
        }

        public AsyncSequence<T> Task { get; }

        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine => stateMachine.MoveNext();

        public void SetException(Exception ex) => Task.SetException(ExceptionDispatchInfo.Capture(ex));

        public void SetResult(T value) => Task.SetCompletion();

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            // The requirement for this cast is ridiculous.
            if (awaiter is TaskProvider<IAsyncSequenceProducer<T>>.TaskProviderAwaiter provider)
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
    }
}