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
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoQueryEvaluator<T> : IAsyncEnumerable<T>
    {
        private readonly Func<CancellationToken, ValueTask<IAsyncCursor<T>?>> _asyncCursorSource;
        private readonly Action<IAsyncCursor<T>?>? _asyncCursorDisposal;

        public MongoQueryEvaluator(ValueTask<IAsyncCursorSource<T>> asyncCursorSource)
        {
            async ValueTask<IAsyncCursor<T>?> AsyncCursorSource(CancellationToken cancellation)
            {
                var mongoCursorSource = await asyncCursorSource.ConfigureAwait(false);
                return await mongoCursorSource.ToCursorAsync(cancellation).ConfigureAwait(false);
            }

            _asyncCursorSource = AsyncCursorSource;
        }

        public MongoQueryEvaluator(
            Func<CancellationToken, ValueTask<IAsyncCursor<T>?>> asyncCursorSource,
            Action<IAsyncCursor<T>?>? asyncCursorDisposal = null)
        {
            if (asyncCursorSource == null)
                throw new ArgumentNullException(nameof(asyncCursorSource));

            _asyncCursorSource = asyncCursorSource;
            _asyncCursorDisposal = asyncCursorDisposal;
        }

        public MongoQueryEvaluator(
            Task<IMongoCollection<T>?> collectionFuture,
            Expression<Func<T, bool>> predicate)
        {
            if (collectionFuture == null)
                throw new ArgumentNullException(nameof(collectionFuture));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            async ValueTask<IAsyncCursor<T>?> AsyncCursorSource(CancellationToken cancellation)
            {
                var collection = await collectionFuture.ConfigureAwait(false);

                if (collection is null)
                    return null;

                return await collection
                    .FindAsync<T>(predicate, cancellationToken: cancellation)
                    .ConfigureAwait(false);
            }

            _asyncCursorSource = AsyncCursorSource;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new MongoQueryResult<T>(_asyncCursorSource, _asyncCursorDisposal, cancellationToken);
        }
    }

    public sealed class MongoQueryResult<T> : IAsyncEnumerator<T>
    {
        private ValueTask<IAsyncCursor<T>?> _asyncCursor;
        private bool _asyncCursorInitialized = false;
        private readonly Func<CancellationToken, ValueTask<IAsyncCursor<T>?>>? _asyncCursorSource;
        private readonly Action<IAsyncCursor<T>?>? _asyncCursorDisposal;
        private readonly CancellationToken _cancellation;
        private IAsyncCursor<T>? _asyncCursorInstance;
        private IEnumerator<T>? _currentBatch;
        private bool _endOfSeq = false;
        private bool _isDisposed = false;

        public MongoQueryResult(
            Func<CancellationToken, ValueTask<IAsyncCursor<T>?>> asyncCursorSource,
            Action<IAsyncCursor<T>?>? asyncCursorDisposal = null,
            CancellationToken cancellation = default)
        {
            if (asyncCursorSource == null)
                throw new ArgumentNullException(nameof(asyncCursorSource));

            _asyncCursorSource = asyncCursorSource;
            _asyncCursorDisposal = asyncCursorDisposal;
            _cancellation = cancellation;
        }

        public MongoQueryResult(IAsyncCursor<T> asyncCursor)
        {
            if (asyncCursor == null)
                throw new ArgumentNullException(nameof(asyncCursor));

            _asyncCursor = new ValueTask<IAsyncCursor<T>?>(asyncCursor);
            _asyncCursorInitialized = true;
        }

        [MaybeNull]
        public T Current
        {
            get
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                if (_asyncCursorInstance == null)
                    return default!;

                if (_currentBatch == null)
                    return default!;

                return _currentBatch.Current;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                _currentBatch?.Dispose();

                if (_asyncCursorDisposal != null)
                {
                    _asyncCursorDisposal(_asyncCursorInstance);
                }
                else if (_asyncCursorInstance != null)
                {
                    _asyncCursorInstance.Dispose();
                }
            }

            return default;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (_endOfSeq)
                return false;

            if (!_asyncCursorInitialized)
            {
                _asyncCursorInitialized = true;
                _asyncCursor = _asyncCursorSource!.Invoke(_cancellation);
            }

            if (_asyncCursorInstance == null)
            {
                _asyncCursorInstance = await _asyncCursor;

                if (_asyncCursorInstance == null)
                {
                    _endOfSeq = true;
                    return false;
                }
            }

            while (_currentBatch == null || !_currentBatch.MoveNext())
            {
                _currentBatch?.Dispose();

                // TODO: Catch all exceptions and abort the transaction
                if (!await MongoExceptionHelper.TryWriteOperation(
                    () => _asyncCursorInstance.MoveNextAsync(_cancellation)).ConfigureAwait(false))
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
