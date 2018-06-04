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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Async
{
    public static class AsyncEnumerableExtension
    {
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
                Debug.Assert(enumerable != null);
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
                    Debug.Assert(enumerable != null);
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
                Debug.Assert(enumerable != null);
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
}
