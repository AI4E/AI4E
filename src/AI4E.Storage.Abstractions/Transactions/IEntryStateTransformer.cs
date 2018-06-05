using System;
using System.Collections.Generic;
using System.Linq;

namespace AI4E.Storage.Transactions
{
    public interface IEntryStateTransformer<TId, TData>
        where TData : class
    {
        IEntryState<TId, TData> Create(TId id, TData data, long transactionId);

        IEntryState<TId, TData> AddPendingTransaction(IEntryState<TId, TData> entry, long transactionId);

        IEntryState<TId, TData> Store(IEntryState<TId, TData> entry, TData data, long transactionId);

        IEntryState<TId, TData> Abort(IEntryState<TId, TData> entry, long transactionId);

        IEntryState<TId, TData> Commit(IEntryState<TId, TData> entry, long transactionId);

        IEntrySnapshot<TId, TData> ToSnapshot(IEntryState<TId, TData> entry);
    }

    public interface IEntryStateTransformerFactory
    {
        IEntryStateTransformer<TId, TData> GetEntryManager<TId, TData>() where TData : class;
    }

    public static class IEntryStateTransformerExtension
    {
        public static IEntryState<TId, TData> CommitAll<TId, TData>(this IEntryStateTransformer<TId, TData> entryManager, IEntryState<TId, TData> entry, IEnumerable<long> transactionIds)
            where TData : class
        {
            if (entryManager == null)
                throw new ArgumentNullException(nameof(entryManager));

            if (transactionIds == null)
                throw new ArgumentNullException(nameof(transactionIds));

            return transactionIds.Aggregate(seed: entry, (current, transactionId) => entryManager.Commit(current, transactionId));
        }
    }
}
