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

using System.Collections.Immutable;
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
    }
}
