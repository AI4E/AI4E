using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    }
}
