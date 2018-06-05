using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Transactions
{
    public interface ITransactionalDatabase
    {
        Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class;

        Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class;

        // Returns all entries of type 'TData' where predicate matches.
        Task<IEnumerable<TData>> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class;

        Task<bool> TryCommitAsync(CancellationToken cancellation = default);
        Task RollbackAsync(CancellationToken cancellation = default);
    }
}
