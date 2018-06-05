using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Transactions
{
    public interface ITransaction
    {
        long Id { get; }

        Task AbortAsync(CancellationToken cancellation);
        Task CleanUp(CancellationToken cancellation);
        Task<IOperation> DeleteOperationAsync<TData>(TData data, int? expectedVersion, CancellationToken cancellation);
        Task EnsureExistenceAsync(CancellationToken cancellation);
        Task<ImmutableArray<IOperation>> GetOperationsAsync(CancellationToken cancellation);
        ValueTask<ImmutableArray<IOperation>> GetOperationsFromCacheAsync(CancellationToken cancellation);
        Task<TransactionStatus?> GetStateAsync(CancellationToken cancellation);
        Task<IOperation> StoreOperationAsync<TData>(TData data, int? expectedVersion, CancellationToken cancellation);
        Task<bool> TryApplyOperationAsync(IOperation operation, CancellationToken cancellation);
        Task<bool> TryBeginCommitAsync(CancellationToken cancellation);
        Task<bool> TryCommitAsync(CancellationToken cancellation);
        Task<bool> TryPrepare(CancellationToken cancellation);
        Task<bool> TryRequestAbortAsync(CancellationToken cancellation);
        Task<bool> TryUnapplyOperationAsync(IOperation operation, CancellationToken cancellation);
    }
}