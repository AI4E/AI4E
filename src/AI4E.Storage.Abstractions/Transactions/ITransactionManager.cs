using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Transactions
{
    public interface ITransactionManager
    {
        ValueTask<ITransaction> CreateTransactionAsync(CancellationToken cancellation);
        IAsyncEnumerable<ITransaction> GetNonCommittedTransactionsAsync(CancellationToken cancellation);
        ValueTask<ITransaction> GetTransactionAsync(long id, CancellationToken cancellation);
    }
}
