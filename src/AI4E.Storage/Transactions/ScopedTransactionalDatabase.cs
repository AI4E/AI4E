using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Storage.Transactions
{
    public sealed class ScopedTransactionalDatabase : IScopedTransactionalDatabase
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ILogger<ScopedTransactionalDatabase> _logger;

        private ITransaction _transaction;
        private readonly AsyncLock _lock = new AsyncLock();

        public ScopedTransactionalDatabase(ITransactionManager transactionManager, ILogger<ScopedTransactionalDatabase> logger = null)
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            _transactionManager = transactionManager;
            _logger = logger;
        }

        public async Task StoreAsync<TData>(TData data, CancellationToken cancellation)
            where TData : class
        {
            var transaction = await GetTransactionAsync(cancellation);

            await transaction.StoreAsync(data, cancellation);
        }

        public async Task RemoveAsync<TData>(TData data, CancellationToken cancellation)
            where TData : class
        {
            var transaction = await GetTransactionAsync(cancellation);

            await transaction.RemoveAsync(data, cancellation);
        }

        public IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
            where TData : class
        {
            return new GetAsyncEnumerable<TData>(this, predicate, cancellation);
        }

        public async Task<bool> TryCommitAsync(CancellationToken cancellation)
        {
            ITransaction transaction;

            using (await _lock.LockAsync(cancellation))
            {
                transaction = _transaction;
                _transaction = null;
            }

            if (transaction == null)
            {
                return true;
            }

            return await transaction.TryCommitAsync(cancellation);
        }

        public async Task RollbackAsync(CancellationToken cancellation)
        {
            ITransaction transaction;

            using (await _lock.LockAsync(cancellation))
            {
                transaction = _transaction;
                _transaction = null;
            }

            if (transaction != null)
            {
                await transaction.AbortAsync(cancellation);
            }
        }

        private volatile int _isDisposed = 0;

        public void Dispose()
        {
            var isDisposed = Interlocked.Exchange(ref _isDisposed, 1);

            if (isDisposed == 0)
            {
                RollbackAsync(cancellation: default).HandleExceptions(_logger);
            }
        }

        private async Task<ITransaction> GetTransactionAsync(CancellationToken cancellation)
        {
            ITransaction transaction;

            if (_isDisposed != 0)
                throw new ObjectDisposedException(GetType().FullName);

            using (await _lock.LockAsync(cancellation))
            {
                transaction = _transaction;

                if (transaction == null)
                {
                    _transaction = await _transactionManager.CreateTransactionAsync(cancellation);
                    transaction = _transaction;
                }
            }

            return transaction;
        }

        private sealed class GetAsyncEnumerable<TData> : IAsyncEnumerable<TData>
            where TData : class
        {
            private readonly ScopedTransactionalDatabase _db;
            private readonly Expression<Func<TData, bool>> _predicate;
            private readonly CancellationToken _cancellation;

            public GetAsyncEnumerable(ScopedTransactionalDatabase db, Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
            {
                _db = db;
                _predicate = predicate;
                _cancellation = cancellation;
            }

            public IAsyncEnumerator<TData> GetEnumerator()
            {
                return new GetAsyncEnumerator(_db, _predicate, _cancellation);
            }

            private sealed class GetAsyncEnumerator : IAsyncEnumerator<TData>
            {
                private readonly ScopedTransactionalDatabase _db;
                private readonly Expression<Func<TData, bool>> _predicate;
                private readonly CancellationToken _cancellation;

                private IAsyncEnumerator<TData> _enumerator;

                public GetAsyncEnumerator(ScopedTransactionalDatabase db, Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
                {
                    _db = db;
                    _predicate = predicate;
                    _cancellation = cancellation;
                }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    if (_enumerator == null)
                    {
                        using (var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellation))
                        {
                            var transaction = await _db.GetTransactionAsync(combinedCancellationSource.Token);
                            _enumerator = transaction.GetAsync(_predicate, combinedCancellationSource.Token).GetEnumerator();
                        }
                    }

                    return await _enumerator.MoveNext(cancellationToken);
                }

                public TData Current => _enumerator?.Current;

                public void Dispose()
                {
                    _enumerator?.Dispose();
                }
            }
        }
    }
}
