using System;

namespace AI4E.Storage.Transactions
{
    public interface ITransactionStateTransformer
    {
        ITransactionState Create(long id);
        ITransactionState Abort(ITransactionState state);
        ITransactionState AddOperation(ITransactionState state, OperationType operationType, Type entryType, object entry, int? expectedVersion, out IOperation operation);
        ITransactionState Apply(ITransactionState state, IOperation operation);
        ITransactionState BeginCommit(ITransactionState state);
        ITransactionState CleanUp(ITransactionState state);
        ITransactionState Commit(ITransactionState state);
        ITransactionState Prepare(ITransactionState state);
        ITransactionState RemoveOperation(ITransactionState state, IOperation operation);
        ITransactionState RequestAbort(ITransactionState state);
        ITransactionState Unapply(ITransactionState state, IOperation operation);
    }
}