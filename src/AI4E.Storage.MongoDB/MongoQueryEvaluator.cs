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
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoQueryEvaluator<T> : IAsyncEnumerable<T>
    {
        private readonly Func<Task<IAsyncCursor<T>>> _asyncCursorSource;
        private readonly Action<IAsyncCursor<T>> _asyncCursorDisposal;

        public MongoQueryEvaluator(ValueTask<IAsyncCursorSource<T>> asyncCursorSource,
                                   CancellationToken cancellation = default)
        {
            if (asyncCursorSource == null)
                throw new ArgumentNullException(nameof(asyncCursorSource));

            _asyncCursorSource = async () => await (await asyncCursorSource).ToCursorAsync(cancellation);
        }

        public MongoQueryEvaluator(Func<Task<IAsyncCursor<T>>> asyncCursorSource, Action<IAsyncCursor<T>> asyncCursorDisposal = null)
        {
            if (asyncCursorSource == null)
                throw new ArgumentNullException(nameof(asyncCursorSource));

            _asyncCursorSource = asyncCursorSource;
            _asyncCursorDisposal = asyncCursorDisposal;
        }

        public MongoQueryEvaluator(Task<IMongoCollection<T>> collection,
                                   Expression<Func<T, bool>> predicate,
                                   CancellationToken cancellation = default)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            _asyncCursorSource = async () => await (await collection).FindAsync<T>(predicate, cancellationToken: cancellation);
        }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new MongoQueryResult<T>(_asyncCursorSource(), _asyncCursorDisposal);
        }
    }

    public sealed class MongoQueryResult<T> : IAsyncEnumerator<T>
    {
        private readonly ValueTask<IAsyncCursor<T>> _asyncCursor;
        private readonly Action<IAsyncCursor<T>> _asyncCursorDisposal;
        private IAsyncCursor<T> _asyncCursorInstance;
        private IEnumerator<T> _currentBatch;
        private bool _endOfSeq = false;
        private bool _isDisposed = false;

        public MongoQueryResult(Task<IAsyncCursor<T>> asyncCursor, Action<IAsyncCursor<T>> asyncCursorDisposal = null)
        {
            if (asyncCursor == null)
                throw new ArgumentNullException(nameof(asyncCursor));

            _asyncCursor = new ValueTask<IAsyncCursor<T>>(asyncCursor);
            _asyncCursorDisposal = asyncCursorDisposal;
        }

        public MongoQueryResult(IAsyncCursor<T> asyncCursor)
        {
            if (asyncCursor == null)
                throw new ArgumentNullException(nameof(asyncCursor));

            _asyncCursor = new ValueTask<IAsyncCursor<T>>(asyncCursor);
        }

        public T Current
        {
            get
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (_asyncCursorInstance == null)
                    return default;

                if (_currentBatch == null)
                    return default;

                return _currentBatch.Current;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                _currentBatch?.Dispose();

                if (_asyncCursorInstance != null)
                {
                    if (_asyncCursorDisposal != null)
                    {
                        _asyncCursorDisposal(_asyncCursorInstance);
                    }
                    else
                    {
                        _asyncCursorInstance.Dispose();
                    }
                }
            }
        }

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (_endOfSeq)
                return false;

            if (_asyncCursorInstance == null)
            {
                _asyncCursorInstance = await _asyncCursor;
            }

            while (_currentBatch == null || !_currentBatch.MoveNext())
            {
                _currentBatch?.Dispose();

                if (!await _asyncCursorInstance.MoveNextAsync(cancellationToken))
                {
                    _endOfSeq = true;
                    return false;
                }

                var currentBatch = _asyncCursorInstance.Current;

                _currentBatch = currentBatch.GetEnumerator();
            }

            return true;
        }
    }
}
