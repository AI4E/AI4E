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

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Collections.Generic
{
    public static class AI4EUtilsAsyncEnumerableExtensions
    {
        public static ValueTaskAwaiter<IEnumerable<T>> GetAwaiter<T>(this IAsyncEnumerable<T> asyncEnumerable)
        {
            async ValueTask<IEnumerable<T>> Preevaluate()
            {
                var builder = ImmutableList.CreateBuilder<T>();

                await foreach (var t in asyncEnumerable.ConfigureAwait(false))
                {
                    builder.Add(t);
                }

                return builder.ToImmutable();
            }

            return Preevaluate().GetAwaiter();
        }

        #region Cache

        public static IAsyncEnumerable<TItem> Cached<TItem>(this IAsyncEnumerable<TItem> source)
        {
            return new CachedAsyncEnumerable<TItem>(source);
        }

        private sealed class CachedAsyncEnumerable<TItem> : IAsyncEnumerable<TItem>
        {
            private readonly IAsyncEnumerable<TItem> _source;
            private readonly List<TItem> _cache = new List<TItem>();

            private IAsyncEnumerator<TItem>? _enumerator;

            public CachedAsyncEnumerable(IAsyncEnumerable<TItem> source)
            {
                if (source is null)
                    throw new ArgumentNullException(nameof(source));

                _source = source;
            }

            public async IAsyncEnumerator<TItem> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                foreach (var item in _cache)
                {
                    yield return item;
                }

                _enumerator ??= _source.GetAsyncEnumerator();

                while (await _enumerator.MoveNextAsync().ConfigureAwait(true))
                {
                    _cache.Add(_enumerator.Current);
                    yield return _enumerator.Current;
                }
            }
        }

        #endregion

        #region Catch

        public static IAsyncEnumerable<T> Catch<T, TException>(
            this IAsyncEnumerable<T> asyncEnumerable,
            Action<TException> catchClause) where TException : Exception
        {
            if (asyncEnumerable is null)
                throw new ArgumentNullException(nameof(asyncEnumerable));

            if (catchClause is null)
                throw new ArgumentNullException(nameof(catchClause));

            return new CatchAsyncEnumerable<T, TException>(asyncEnumerable, exception =>
            {
                catchClause(exception);
                return new CatchClauseResult<T>(moveNext: true, skip: true, current: default);
            });
        }

        public static IAsyncEnumerable<T> Catch<T, TException>(
            this IAsyncEnumerable<T> asyncEnumerable,
            Func<TException, T> catchClause) where TException : Exception
        {
            if (asyncEnumerable is null)
                throw new ArgumentNullException(nameof(asyncEnumerable));

            if (catchClause is null)
                throw new ArgumentNullException(nameof(catchClause));

            return new CatchAsyncEnumerable<T, TException>(asyncEnumerable, exception =>
            {
                var current = catchClause(exception);
                return new CatchClauseResult<T>(moveNext: true, skip: false, current);
            });
        }

        public static IAsyncEnumerable<T> Catch<T, TException>(
            this IAsyncEnumerable<T> asyncEnumerable,
            Func<TException, bool> catchClause) where TException : Exception
        {
            if (asyncEnumerable is null)
                throw new ArgumentNullException(nameof(asyncEnumerable));

            if (catchClause is null)
                throw new ArgumentNullException(nameof(catchClause));

            return new CatchAsyncEnumerable<T, TException>(asyncEnumerable, exception =>
            {
                var moveNext = catchClause(exception);
                return new CatchClauseResult<T>(moveNext, skip: true, current: default);
            });
        }

        public static IAsyncEnumerable<T> Catch<T, TException>(
          this IAsyncEnumerable<T> asyncEnumerable,
          Func<TException, CatchClauseResult<T>> catchClause) where TException : Exception
        {
            if (asyncEnumerable is null)
                throw new ArgumentNullException(nameof(asyncEnumerable));

            if (catchClause is null)
                throw new ArgumentNullException(nameof(catchClause));

            return new CatchAsyncEnumerable<T, TException>(asyncEnumerable, catchClause);
        }

        public readonly struct CatchClauseResult<T>
        {
            public CatchClauseResult(bool moveNext, bool skip, [AllowNull] T current)
            {
                MoveNext = moveNext;
                Skip = skip;
                Current = current;
            }

            public bool MoveNext { get; }

            public bool Skip { get; }

            [MaybeNull, AllowNull]
            public T Current { get; }

            public void Deconstruct(out bool moveNext, out bool skip, [MaybeNull] out T current)
            {
                moveNext = MoveNext;
                skip = Skip;
                current = Current;
            }
        }

        private sealed class CatchAsyncEnumerable<T, TException> : IAsyncEnumerable<T>
            where TException : Exception
        {
            private readonly IAsyncEnumerable<T> _asyncEnumerable;
            private readonly Func<TException, CatchClauseResult<T>> _catchClause;

            public CatchAsyncEnumerable(
                IAsyncEnumerable<T> asyncEnumerable,
                Func<TException, CatchClauseResult<T>> catchClause)
            {
                _asyncEnumerable = asyncEnumerable;
                _catchClause = catchClause;
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
            {
                return new Enumerator(_asyncEnumerable.GetAsyncEnumerator(cancellationToken), _catchClause);
            }

            private sealed class Enumerator : IAsyncEnumerator<T>
            {
                private readonly IAsyncEnumerator<T> _asyncEnumerator;
                private readonly Func<TException, CatchClauseResult<T>> _catchClause;
                private bool _lastResult = true;

                public Enumerator(
                    IAsyncEnumerator<T> asyncEnumerator,
                    Func<TException, CatchClauseResult<T>> catchClause)
                {
                    _asyncEnumerator = asyncEnumerator;
                    _catchClause = catchClause;
                    Current = default!;
                }

                public async ValueTask<bool> MoveNextAsync()
                {
                    if (!_lastResult)
                    {
                        return false;
                    }

                    bool result;
                    bool skip;

                    do
                    {
                        try
                        {
                            result = await _asyncEnumerator.MoveNextAsync().ConfigureAwait(false);

                            if (result)
                            {
                                Current = _asyncEnumerator.Current;
                            }

                            skip = false;
                        }
                        catch (TException exception)
                        {
                            try
                            {
                                (result, skip, Current) = _catchClause(exception);
                            }
                            catch
                            {
                                _lastResult = false;
                                throw;
                            }
                        }
                    }
                    while (skip && result); // Only skip to the next if moveNext was true

                    return _lastResult = result;
                }

                [AllowNull]
                public T Current { get; private set; }

                public ValueTask DisposeAsync()
                {
                    return _asyncEnumerator.DisposeAsync();
                }
            }
        }

        #endregion
    }
}
