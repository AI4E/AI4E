using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class AsyncEnumerableExtensions
    {
        // Performs an ordinary select except when an exception occurs in the selector, than it ignores the exception and continues.
        public static IAsyncEnumerable<TResult> SelectOrContinue<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> asyncSelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (asyncSelector == null)
                throw new ArgumentNullException(nameof(asyncSelector));

            return new AsyncSelectEnumerable<TSource, TResult>(source, asyncSelector);

        }

        private sealed class AsyncSelectEnumerable<TSource, TResult> : IAsyncEnumerable<TResult>
        {
            private readonly IAsyncEnumerable<TSource> _source;
            private readonly Func<TSource, Task<TResult>> _asyncSelector;

            public AsyncSelectEnumerable(IAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> asyncSelector)
            {
                _source = source;
                _asyncSelector = asyncSelector;
            }

            public IAsyncEnumerator<TResult> GetEnumerator()
            {
                return new AsyncSelectEnumerator(_source, _asyncSelector);
            }

            private sealed class AsyncSelectEnumerator : IAsyncEnumerator<TResult>
            {
                private readonly IAsyncEnumerator<TSource> _enumerator;
                private readonly Func<TSource, Task<TResult>> _asyncSelector;

                public AsyncSelectEnumerator(IAsyncEnumerable<TSource> source, Func<TSource, Task<TResult>> asyncSelector)
                {
                    _enumerator = source.GetEnumerator();
                    _asyncSelector = asyncSelector;

                    Current = default;
                }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    bool result;

                    do
                    {
                        result = await _enumerator.MoveNext(cancellationToken);

                        if (result)
                        {
                            try
                            {
                                Current = await _asyncSelector(_enumerator.Current);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                        else
                        {
                            Current = default;
                        }

                        break;
                    }
                    while (result);

                    return result;
                }

                public TResult Current { get; private set; }

                public void Dispose()
                {
                    _enumerator.Dispose();
                }
            }
        }

        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(in this ValueTask<T> task)
        {
            return new SingleAsyncEnumerable<T>(task);
        }

        private sealed class SingleAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly ValueTask<T> _task;

            public SingleAsyncEnumerable(in ValueTask<T> task)
            {
                _task = task;
            }

            public IAsyncEnumerator<T> GetEnumerator()
            {
                return new SingleAsyncEnumerator(_task);
            }

            private sealed class SingleAsyncEnumerator : IAsyncEnumerator<T>
            {
                private readonly ValueTask<T> _task;
                private bool _initialized = false;

                public SingleAsyncEnumerator(in ValueTask<T> task)
                {
                    _task = task;
                }

                public T Current { get; private set; }

                public void Dispose() { }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    if (_initialized)
                    {
                        Current = default;
                        return false;
                    }

                    _initialized = true;

                    Current = await _task;
                    return true;
                }
            }
        }

        public static TaskAwaiter<T[]> GetAwaiter<T>(this IAsyncEnumerable<T> asyncEnumerable)
        {
            if (asyncEnumerable == null)
                throw new ArgumentNullException(nameof(asyncEnumerable));

            return asyncEnumerable.ToArray().GetAwaiter();
        }

        public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this Task<IEnumerable<T>> enumerable)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable));

            return new ComputedAsyncEnumerable<T>(enumerable);
        }

        private sealed class ComputedAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly Task<IEnumerable<T>> _enumerable;

            public ComputedAsyncEnumerable(Task<IEnumerable<T>> enumerable)
            {
                Assert(enumerable != null);
                _enumerable = enumerable;
            }

            public IAsyncEnumerator<T> GetEnumerator()
            {
                return new ComputedAsyncEnumerator(_enumerable);
            }

            private sealed class ComputedAsyncEnumerator : IAsyncEnumerator<T>
            {
                private readonly Task<IEnumerable<T>> _enumerable;
                private IEnumerator<T> _enumerator;
                private bool _isDisposed = false;


                public ComputedAsyncEnumerator(Task<IEnumerable<T>> enumerable)
                {
                    Assert(enumerable != null);
                    _enumerable = enumerable;
                }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    ThrowIfDisposed();

                    if (_enumerator == null)
                    {
                        _enumerator = (await _enumerable).GetEnumerator();
                    }

                    return _enumerator.MoveNext();
                }

                public T Current => ThrowIfDisposed(_enumerator == null ? default : _enumerator.Current);

                public void Dispose()
                {
                    if (_isDisposed)
                        return;

                    _isDisposed = true;
                    _enumerator?.Dispose();
                }

                private void ThrowIfDisposed()
                {
                    if (_isDisposed)
                        throw new ObjectDisposedException(GetType().FullName);
                }

                private Q ThrowIfDisposed<Q>(Q arg)
                {
                    ThrowIfDisposed();
                    return arg;
                }
            }
        }

        public static IAsyncEnumerable<T> Evaluate<T>(this IAsyncEnumerable<Task<T>> enumerable)
        {
            if (enumerable == null)
                throw new ArgumentNullException(nameof(enumerable));

            return new EvaluationAsyncEnumerable<T>(enumerable);
        }

        private sealed class EvaluationAsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IAsyncEnumerable<Task<T>> _enumerable;

            public EvaluationAsyncEnumerable(IAsyncEnumerable<Task<T>> enumerable)
            {
                Assert(enumerable != null);
                _enumerable = enumerable;
            }

            public IAsyncEnumerator<T> GetEnumerator()
            {
                return new EvaluationAsyncEnumerator(_enumerable);
            }

            private sealed class EvaluationAsyncEnumerator : IAsyncEnumerator<T>
            {
                private readonly IAsyncEnumerator<Task<T>> _enumerator;

                public EvaluationAsyncEnumerator(IAsyncEnumerable<Task<T>> enumerable)
                {
                    _enumerator = enumerable.GetEnumerator();
                }

                public T Current { get; private set; }

                public void Dispose()
                {
                    _enumerator.Dispose();
                }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    if (!await _enumerator.MoveNext(cancellationToken))
                    {
                        return false;
                    }

                    Current = await _enumerator.Current;
                    return true;
                }
            }
        }
    }

    internal static class AsyncEnumerableHelper
    {
        public static IAsyncEnumerable<TResult> Generate<TState, TResult>(TState initialState,
                                                                          Func<TState, Task<bool>> condition,
                                                                          Func<TState, ValueTask<TState>> iterate,
                                                                          Func<TState, ValueTask<TResult>> resultSelector)
        {
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            if (iterate == null)
                throw new ArgumentNullException(nameof(iterate));

            if (resultSelector == null)
                throw new ArgumentNullException(nameof(resultSelector));

            return new GeneratedAsyncEnumerable<TState, TResult>(initialState, condition, iterate, resultSelector);
        }

        private sealed class GeneratedAsyncEnumerable<TState, TResult> : IAsyncEnumerable<TResult>
        {
            private readonly TState _initialState;
            private readonly Func<TState, Task<bool>> _condition;
            private readonly Func<TState, ValueTask<TState>> _iterate;
            private readonly Func<TState, ValueTask<TResult>> _resultSelector;

            public GeneratedAsyncEnumerable(TState initialState,
                                            Func<TState, Task<bool>> condition,
                                            Func<TState, ValueTask<TState>> iterate,
                                            Func<TState, ValueTask<TResult>> resultSelector)
            {
                Assert(condition != null);
                Assert(iterate != null);
                Assert(resultSelector != null);
                _initialState = initialState;
                _condition = condition;
                _iterate = iterate;
                _resultSelector = resultSelector;
            }

            public IAsyncEnumerator<TResult> GetEnumerator()
            {
                return new GeneratedAsyncEnumerator<TState, TResult>(_initialState, _condition, _iterate, _resultSelector);
            }
        }

        private sealed class GeneratedAsyncEnumerator<TState, TResult> : IAsyncEnumerator<TResult>
        {
            private readonly Func<TState, Task<bool>> _condition;
            private readonly Func<TState, ValueTask<TState>> _iterate;
            private readonly Func<TState, ValueTask<TResult>> _resultSelector;

            private TState _state;
            private bool _completed;

            public GeneratedAsyncEnumerator(TState initialState,
                                            Func<TState, Task<bool>> condition,
                                            Func<TState, ValueTask<TState>> iterate,
                                            Func<TState, ValueTask<TResult>> resultSelector)
            {
                Assert(condition != null);
                Assert(iterate != null);
                Assert(resultSelector != null);

                _state = initialState;
                _condition = condition;
                _iterate = iterate;
                _resultSelector = resultSelector;
            }

            public TResult Current { get; private set; }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (_completed || !await _condition(_state))
                {
                    _completed = true;
                    return false;
                }

                _state = await _iterate(_state);
                Current = await _resultSelector(_state);
                return true;
            }

            public void Dispose()
            {
                _completed = true;
            }
        }
    }
}
