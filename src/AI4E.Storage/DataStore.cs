using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Transactions;

namespace AI4E.Storage
{
    public sealed class DataStore : IDataStore
    {
        private readonly ITransactionManager _transactionManager;

        public DataStore(ITransactionManager transactionManager)
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            _transactionManager = transactionManager;
        }

        public Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            return _transactionManager.UnconditionalWriteAsync((db, _) => db.RemoveAsync(data, cancellation), cancellation);
        }

        public Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            return _transactionManager.UnconditionalWriteAsync((db, _) => db.StoreAsync(data, cancellation), cancellation);
        }

        public IAsyncEnumerable<TData> AllAsync<TData>(CancellationToken cancellation = default)
            where TData : class
        {
            return FindAsync<TData>(p => true, cancellation);
        }

        public IAsyncEnumerable<TData> FindAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class
        {
            return _transactionManager.UnconditionalReadAsync(predicate, cancellation);
        }

        public ValueTask<TData> FindOneAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class
        {
            return new ValueTask<TData>(FindAsync(predicate, cancellation).FirstOrDefault());
        }

        public ValueTask<TData> OneAsync<TData>(CancellationToken cancellation = default)
            where TData : class
        {
            return FindOneAsync<TData>(p => true, cancellation);
        }

        public void Dispose() { }
    }
}
