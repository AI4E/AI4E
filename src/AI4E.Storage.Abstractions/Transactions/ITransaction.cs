using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Transactions
{
    public interface ITransaction
    {
        long Id { get; }
        ImmutableArray<IOperation> Operations { get; }
        bool OwnsTransaction { get; }
        TransactionStatus? Status { get; }
        ITransactionState UnderlyingState { get; }

        Task AbortAsync(CancellationToken cancellation = default);
        IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default) where TData : class;
        Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default) where TData : class;
        Task StoreAsync<TData>(TData data, CancellationToken cancellation = default) where TData : class;
        Task<bool> TryCommitAsync(CancellationToken cancellation = default);
        Task<ITransactionState> UpdateAsync(CancellationToken cancellation = default);
    }
}
