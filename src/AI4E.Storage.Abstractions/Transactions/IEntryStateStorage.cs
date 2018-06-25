using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Transactions
{
    public interface IEntryStateStorage<TId, TData> where TData : class
    {
        IAsyncEnumerable<IEntryState<TId, TData>> GetEntriesAsync(Expression<Func<IEntryState<TId, TData>, bool>> predicate, 
                                                                  CancellationToken cancellation = default);

        Task<bool> CompareExchangeAsync(IEntryState<TId, TData> entry, 
                                        IEntryState<TId, TData> comparand, 
                                        CancellationToken cancellation = default);
    }

    public interface IEntryStateStorageFactory
    {
        IEntryStateStorage<TId, TData> GetEntryStorage<TId, TData>() where TData : class;
    }
}
