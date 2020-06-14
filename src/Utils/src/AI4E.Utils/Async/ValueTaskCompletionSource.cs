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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.ObjectPool;

namespace AI4E.Utils.Async
{
    /// <summary>
    /// Represents the producer side of of a <see cref="ValueTask{TResult}"/>
    /// providing access to the consumer side with through the <see cref="Task"/> property.
    /// </summary>
    /// <typeparam name="T">The type of result value.</typeparam>
    public readonly struct ValueTaskCompletionSource<T> : IEquatable<ValueTaskCompletionSource<T>>
    {
        private readonly ValueTaskSource<T> _source;
        private readonly short _token;

        private ValueTaskCompletionSource(ValueTaskSource<T> source)
        {
            Debug.Assert(source != null);
            Debug.Assert(!source!.Exhausted);

            var token = source.Token;

            _source = source;
            _token = token;
            Task = new ValueTask<T>(source, token);
        }

        /// <summary>
        /// Gets a <see cref="ValueTask{TResult}"/> created by the <see cref="ValueTaskCompletionSource{T}"/>.
        /// </summary>
        public ValueTask<T> Task { get; }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask{TResult}"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetCanceled()
        {
            return TrySetCanceled(cancellation: default);
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask{TResult}"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/>.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetCanceled(CancellationToken cancellation)
        {
            return _source?.TryNotifyCompletion(cancellation, _token) ?? false;
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask{TResult}"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> that caused the task to fail.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetException(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return _source?.TryNotifyCompletion(exception, _token) ?? false;
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask{TResult}"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exceptions">The collection of<see cref="Exception"/>s that caused the task to fail.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetException(IEnumerable<Exception> exceptions)
        {
            if (exceptions == null)
                throw new ArgumentNullException(nameof(exceptions));

            var exception = exceptions.FirstOrDefault();

            if (exception == null)
            {
                if (!exceptions.Any())
                    throw new ArgumentException("The collection must not be empty.", nameof(exceptions));

                throw new ArgumentException("The collection must not contain null entries.", nameof(exceptions));
            }

            return TrySetException(exception);
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask{TResult}"/>
        /// to the <c>CompletedSuccessfully</c> state.
        /// </summary>
        /// <param name="result">The result value to bind to the <see cref="ValueTask{TResult}"/>.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetResult(T result)
        {
            return _source?.TryNotifyCompletion(result, _token) ?? false;
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask{TResult}"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask{TResult}"/> is already completed.
        /// </exception>
        public void SetCanceled()
        {
            if (!TrySetCanceled())
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask{TResult}"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <param name="cancellation">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask{TResult}"/> is already completed.
        /// </exception>
        public void SetCanceled(CancellationToken cancellation)
        {
            if (!TrySetCanceled(cancellation))
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask{TResult}"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> that caused the task to fail.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask{TResult}"/> is already completed.
        /// </exception>
        public void SetException(Exception exception)
        {
            if (!TrySetException(exception))
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask{TResult}"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exceptions">The collection of<see cref="Exception"/>s that caused the task to fail.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask{TResult}"/> is already completed.
        /// </exception>
        public void SetException(IEnumerable<Exception> exceptions)
        {
            if (!TrySetException(exceptions))
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask{TResult}"/> to the <c>CompletedSuccessfully</c> state.
        /// </summary>
        /// <param name="result">The result value to bind to the <see cref="ValueTask{TResult}"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask{TResult}"/> is already completed.
        /// </exception>
        public void SetResult(T result)
        {
            if (!TrySetResult(result))
            {
                ThrowAlreadyCompleted();
            }
        }

        private static void ThrowAlreadyCompleted()
        {
            throw new InvalidOperationException("An attempt was made to transition a value task to a final state when it had already completed");
        }

        internal static ValueTaskCompletionSource<T> Create()
        {
            var source = ValueTaskSource<T>.Allocate();
            return new ValueTaskCompletionSource<T>(source);
        }

        /// <inheritdoc/>
        public bool Equals(ValueTaskCompletionSource<T> other)
        {
            return _source == other._source && _token == other._token;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is ValueTaskCompletionSource<T> valueTaskCompletionSource && Equals(valueTaskCompletionSource);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (_source, _token).GetHashCode();
        }

        /// <summary>
        /// Gets a boolean value indicating whether two <see cref="ValueTaskCompletionSource{T}"/> are equal.
        /// </summary>
        /// <param name="left">The first <see cref="ValueTaskCompletionSource{T}"/>.</param>
        /// <param name="right">The second <see cref="ValueTaskCompletionSource{T}"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in ValueTaskCompletionSource<T> left, in ValueTaskCompletionSource<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Gets a boolean value indicating whether two <see cref="ValueTaskCompletionSource{T}"/> are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="ValueTaskCompletionSource{T}"/>.</param>
        /// <param name="right">The second <see cref="ValueTaskCompletionSource{T}"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in ValueTaskCompletionSource<T> left, in ValueTaskCompletionSource<T> right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Represents the producer side of of a <see cref="ValueTask"/>
    /// providing access to the consumer side with through the <see cref="Task"/> property.
    /// </summary>
    public readonly struct ValueTaskCompletionSource : IEquatable<ValueTaskCompletionSource>
    {
        private readonly ValueTaskSource<byte> _source;
        private readonly short _token;

        private ValueTaskCompletionSource(ValueTaskSource<byte> source)
        {
            Debug.Assert(source != null);
            Debug.Assert(!source!.Exhausted);

            var token = source.Token;

            _source = source;
            _token = token;
            Task = new ValueTask(source, token);
        }

        /// <summary>
        /// Gets a <see cref="ValueTask"/> created by the <see cref="ValueTaskCompletionSource"/>.
        /// </summary>
        public ValueTask Task { get; }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetCanceled()
        {
            return TrySetCanceled(cancellation: default);
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/>.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetCanceled(CancellationToken cancellation)
        {
            return _source?.TryNotifyCompletion(cancellation, _token) ?? false;
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> that caused the task to fail.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetException(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return _source?.TryNotifyCompletion(exception, _token) ?? false;
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exceptions">The collection of<see cref="Exception"/>s that caused the task to fail.</param>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetException(IEnumerable<Exception> exceptions)
        {
            if (exceptions == null)
                throw new ArgumentNullException(nameof(exceptions));

            var exception = exceptions.FirstOrDefault();

            if (exception == null)
            {
                if (!exceptions.Any())
                    throw new ArgumentException("The collection must not be empty.", nameof(exceptions));

                throw new ArgumentException("The collection must not contain null entries.", nameof(exceptions));
            }

            return TrySetException(exception);
        }

        /// <summary>
        /// Attempts to transition the underlying <see cref="ValueTask"/> to the <c>CompletedSuccessfully</c> state.
        /// </summary>
        /// <returns>A boolean value indicating whether the operation was successful.</returns>
        public bool TrySetResult()
        {
            return _source?.TryNotifyCompletion(0, _token) ?? false;
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask"/> is already completed.
        /// </exception>
        public void SetCanceled()
        {
            if (!TrySetCanceled())
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask"/> to the <c>Canceled</c> state.
        /// </summary>
        /// <param name="cancellation">The <see cref="CancellationToken"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask"/> is already completed.
        /// </exception>
        public void SetCanceled(CancellationToken cancellation)
        {
            if (!TrySetCanceled(cancellation))
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exception">The <see cref="Exception"/> that caused the task to fail.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask"/> is already completed.
        /// </exception>
        public void SetException(Exception exception)
        {
            if (!TrySetException(exception))
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask"/> to the <c>Faulted</c> state.
        /// </summary>
        /// <param name="exceptions">The collection of<see cref="Exception"/>s that caused the task to fail.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask"/> is already completed.
        /// </exception>
        public void SetException(IEnumerable<Exception> exceptions)
        {
            if (!TrySetException(exceptions))
            {
                ThrowAlreadyCompleted();
            }
        }

        /// <summary>
        /// Transitions the underlying <see cref="ValueTask"/> to the <c>CompletedSuccessfully</c> state.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ValueTask"/> is already completed.
        /// </exception>
        public void SetResult()
        {
            if (!TrySetResult())
            {
                ThrowAlreadyCompleted();
            }
        }

        private static void ThrowAlreadyCompleted()
        {
            throw new InvalidOperationException("An attempt was made to transition a value task to a final state when it had already completed");
        }

        /// <summary>
        /// Creates a new <see cref="ValueTaskCompletionSource"/>.
        /// </summary>
        /// <returns>The created <see cref="ValueTaskCompletionSource"/>.</returns>
        public static ValueTaskCompletionSource Create()
        {
            var source = ValueTaskSource<byte>.Allocate();
            return new ValueTaskCompletionSource(source);
        }

        /// <summary>
        /// Creates a new <see cref="ValueTaskCompletionSource{T}"/>.
        /// </summary>
        /// <returns>The created <see cref="ValueTaskCompletionSource{T}"/>.</returns>
        public static ValueTaskCompletionSource<T> Create<T>()
        {
            return ValueTaskCompletionSource<T>.Create();
        }

        /// <inheritdoc/>
        public bool Equals(ValueTaskCompletionSource other)
        {
            return _source == other._source && _token == other._token;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is ValueTaskCompletionSource valueTaskCompletionSource && Equals(valueTaskCompletionSource);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return (_source, _token).GetHashCode();
        }

        /// <summary>
        /// Gets a boolean value indicating whether two <see cref="ValueTaskCompletionSource"/> are equal.
        /// </summary>
        /// <param name="left">The first <see cref="ValueTaskCompletionSource"/>.</param>
        /// <param name="right">The second <see cref="ValueTaskCompletionSource"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in ValueTaskCompletionSource left, in ValueTaskCompletionSource right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Gets a boolean value indicating whether two <see cref="ValueTaskCompletionSource"/> are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="ValueTaskCompletionSource"/>.</param>
        /// <param name="right">The second <see cref="ValueTaskCompletionSource"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in ValueTaskCompletionSource left, in ValueTaskCompletionSource right)
        {
            return !left.Equals(right);
        }
    }

#nullable disable

    // Based on: http://tooslowexception.com/implementing-custom-ivaluetasksource-async-without-allocations/
    internal sealed class ValueTaskSource<T> : IValueTaskSource<T>, IValueTaskSource
    {
        #region Pooling

        private static readonly ObjectPool<ValueTaskSource<T>> _pool
            = new DefaultObjectPool<ValueTaskSource<T>>(ValueTaskSourcePooledObjectPolicy.Instance);

        private static readonly ObjectPool<SynchronizationContextPostState> _synchronizationContextPostStatePool
            = new DefaultObjectPool<SynchronizationContextPostState>(
                new DefaultPooledObjectPolicy<SynchronizationContextPostState>());

        private static readonly ObjectPool<ExecutionContextRunState> _executionContextRunStatePool
            = new DefaultObjectPool<ExecutionContextRunState>(
                new DefaultPooledObjectPolicy<ExecutionContextRunState>());

        private sealed class ValueTaskSourcePooledObjectPolicy : PooledObjectPolicy<ValueTaskSource<T>>
        {
            public static ValueTaskSourcePooledObjectPolicy Instance { get; }
                = new ValueTaskSourcePooledObjectPolicy();

            private ValueTaskSourcePooledObjectPolicy() { }

            public override ValueTaskSource<T> Create()
            {
                return new ValueTaskSource<T>();
            }

            public override bool Return(ValueTaskSource<T> obj)
            {
                return !obj.Exhausted;
            }
        }

        #endregion

        public Action<object> _continuation;
        public T _result;
        public bool _completed;
        public Exception _exception;
        public object _continuationState;
        public ExecutionContext _executionContext;
        public object _scheduler;

        internal bool Exhausted { get; private set; }
        internal short Token { get; private set; }

        internal static ValueTaskSource<T> Allocate()
        {
            var result = _pool.Get();
            Debug.Assert(!result.Exhausted);
            Debug.Assert(EqualityComparer<T>.Default.Equals(result._result, default));
            Debug.Assert(result._exception == null);
            Debug.Assert(result._completed == default);
            Debug.Assert(result._continuation == default);
            Debug.Assert(result._continuationState == null);
            Debug.Assert(result._executionContext == null);
            Debug.Assert(result._scheduler == null);
            return result;
        }

        internal bool TryNotifyCompletion(T result, short token)
        {
            return TrySetCompleted(exception: null, result, token);
        }

#pragma warning disable CA1068, IDE0060, CA1801
        // TODO: Is there a way, we can pass in the cancellation token here?
        internal bool TryNotifyCompletion(CancellationToken cancellation, short token)
#pragma warning restore CA1068, IDE0060, CA1801
        {
            return TrySetCompleted(new TaskCanceledException(), result: default, token);
        }

        internal bool TryNotifyCompletion(Exception exception, short token)
        {
            Debug.Assert(exception != null);

            return TrySetCompleted(exception, result: default, token);
        }

        private bool TrySetCompleted(Exception exception, T result, short token)
        {
            Action<object> continuation;
            object continuationState;
            ExecutionContext executionContext;
            object scheduler;

            // Use this object for locking, as this is safe here (internal type) and we do not need to allocate a mutex object.
            lock (this)
            {
                if (token != Token || _completed)
                {
                    return false;
                }

                _exception = exception;
                _result = result;
                _completed = true;

                Monitor.PulseAll(this);

                continuation = _continuation;
                continuationState = _continuationState;
                executionContext = _executionContext;
                scheduler = _scheduler;
            }

            ExecuteContinuation(continuation, continuationState, executionContext, scheduler, forceAsync: false);

            return true;
        }

        private void ExecuteContinuation(
            Action<object> continuation,
            object continuationState,
            ExecutionContext executionContext,
            object scheduler,
            bool forceAsync)
        {
            if (continuation == null)
                return;

            if (executionContext != null)
            {
                // This case should be relatively rare, as the async Task/ValueTask method builders
                // use the awaiter's UnsafeOnCompleted, so this will only happen with code that
                // explicitly uses the awaiter's OnCompleted instead.

                var executionContextRunState = _executionContextRunStatePool.Get();
                executionContextRunState.ValueTaskSource = this;
                executionContextRunState.Continuation = continuation;
                executionContextRunState.ContinuationState = continuationState;
                executionContextRunState.Scheduler = scheduler;

                static void ExecutionContextCallback(object runState)
                {
                    var t = (ExecutionContextRunState)runState;
                    try
                    {
                        t.ValueTaskSource.ExecuteContinuation(t.Continuation, t.ContinuationState, executionContext: null, t.Scheduler, forceAsync: false);
                    }
                    finally
                    {
                        t.ValueTaskSource = null;
                        t.Continuation = null;
                        t.ContinuationState = null;
                        t.Scheduler = null;
                        _executionContextRunStatePool.Return(t);
                    }
                }

                ExecutionContext.Run(executionContext, ExecutionContextCallback, executionContextRunState);
            }
            else if (scheduler is SynchronizationContext synchronizationContext)
            {
                var synchronizationContextPostState = _synchronizationContextPostStatePool.Get();
                synchronizationContextPostState.Continuation = continuation;
                synchronizationContextPostState.ContinuationState = continuationState;

                static void PostCallback(object s)
                {
                    var t = (SynchronizationContextPostState)s;
                    try
                    {
                        t.Continuation(t.ContinuationState);
                    }
                    finally
                    {
                        t.Continuation = null;
                        t.ContinuationState = null;
                        _synchronizationContextPostStatePool.Return(t);
                    }
                }

                synchronizationContext.Post(PostCallback, synchronizationContextPostState);
            }
            else if (scheduler is TaskScheduler taskScheduler)
            {
                Task.Factory.StartNew(
                    continuation,
                    continuationState,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    taskScheduler);
            }
            else if (forceAsync)
            {
                ExecuteContinuation(continuation, continuationState);
            }
            else
            {
                Debug.Assert(scheduler is null);

                continuation(continuationState);
            }
        }

        private static void ExecuteContinuation(Action<object> continuation, object continuationState)
        {
            var synchronizationContext = SynchronizationContext.Current;

            try
            {
                SynchronizationContext.SetSynchronizationContext(null);

                var threadPoolWorkItem = (WaitCallback)Delegate.CreateDelegate(typeof(WaitCallback), continuation.Target, continuation.Method);
                ThreadPool.QueueUserWorkItem(threadPoolWorkItem, continuationState);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
            }
        }

        #region IValueTaskSource

        public ValueTaskSourceStatus GetStatus(short token)
        {
            bool completed;
            Exception exception;

            // Use this object for locking, as this is safe here (internal type) and we do not need to allocate a mutex object.
            lock (this)
            {
                if (token != Token)
                {
                    ThrowMultipleContinuations();
                }

                completed = _completed;
                exception = _exception;
            }

            if (!completed)
            {
                return ValueTaskSourceStatus.Pending;
            }

            if (exception == null)
            {
                return ValueTaskSourceStatus.Succeeded;
            }

            if (exception is TaskCanceledException)
            {
                return ValueTaskSourceStatus.Canceled;
            }

            return ValueTaskSourceStatus.Faulted;
        }


        private static bool TryGetNonDefaultTaskScheduler(out TaskScheduler taskScheduler)
        {
            taskScheduler = TaskScheduler.Current;

            if (taskScheduler == TaskScheduler.Default)
            {
                taskScheduler = null;
            }

            return taskScheduler != null;
        }

        private static bool TryGetNonDefaultSynchronizationContext(out SynchronizationContext synchronizationContext)
        {
            synchronizationContext = SynchronizationContext.Current;

            if (synchronizationContext != null && synchronizationContext.GetType() == typeof(SynchronizationContext))
            {
                synchronizationContext = null;
            }

            return synchronizationContext != null;
        }

        private object GetScheduler()
        {
            if (TryGetNonDefaultSynchronizationContext(out var synchronizationContext))
            {
                return synchronizationContext;
            }

            if (TryGetNonDefaultTaskScheduler(out var taskScheduler))
            {
                return taskScheduler;
            }

            return null;
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            // Use this object for locking, as this is safe here (internal type) and we do not need to allocate a mutex object.
            lock (this)
            {
                if (token != Token || _continuation != null)
                {
                    ThrowMultipleContinuations();
                }

                if (!_completed)
                {
                    if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
                    {
                        _executionContext = ExecutionContext.Capture();
                    }

                    if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
                    {
                        _scheduler = GetScheduler();
                    }

                    // Remember continuation and state
                    _continuationState = state;
                    _continuation = continuation;
                    return;
                }
            }

            var scheduler = ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0) ? GetScheduler() : null;
            ExecuteContinuation(continuation, state, executionContext: null, scheduler, forceAsync: true);
        }

        public T GetResult(short token)
        {
            Exception exception;
            T result;

            // Use this object for locking, as this is safe here (internal type) and we do not need to allocate a mutex object.
            lock (this)
            {
                // If we are not yet completed, block the current thread until we are.
                if (!_completed)
                {
                    Monitor.Wait(this);
                    Debug.Assert(_completed);
                }

                if (token != Token)
                {
                    ThrowMultipleContinuations();
                }

                exception = _exception;
                result = _result;

                if (Token == short.MaxValue)
                {
                    Exhausted = true;
                }

                Token++;
                _continuation = default;
                _result = default;
                _completed = default;
                _exception = default;
                _continuationState = default;
                _executionContext = default;
                _scheduler = default;
            }

            _pool.Return(this);

            if (exception != null)
            {
                var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                exceptionDispatchInfo.Throw();

                Debug.Fail("This must never be reached.");
                throw exception;
            }

            return result;
        }

        void IValueTaskSource.GetResult(short token)
        {
            GetResult(token);
        }

        #endregion

        private static void ThrowMultipleContinuations()
        {
            throw new InvalidOperationException("Multiple awaiters are not allowed");
        }

        private sealed class SynchronizationContextPostState
        {
            public Action<object> Continuation { get; set; }
            public object ContinuationState { get; set; }
        }

        private sealed class ExecutionContextRunState
        {
            public ValueTaskSource<T> ValueTaskSource { get; set; }
            public Action<object> Continuation { get; set; }
            public object ContinuationState { get; set; }
            public object Scheduler { get; set; }
        }
    }

#nullable enable
}
