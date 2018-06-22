using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Transactions
{
    public interface ITransactionStateStorage
    {
        IAsyncEnumerable<ITransactionState> GetNonCommittedTransactionsAsync(CancellationToken cancellation = default);

        ValueTask<ITransactionState> GetTransactionAsync(long id, CancellationToken cancellation = default);

        ValueTask<long> GetUniqueTransactionIdAsync(CancellationToken cancellation = default);

        Task RemoveAsync(ITransactionState transaction, CancellationToken cancellation = default);

        Task<bool> CompareExchangeAsync(ITransactionState transaction, ITransactionState comparand, CancellationToken cancellation = default);
    }
}
