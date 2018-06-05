using System;
using System.Collections.Immutable;

namespace AI4E.Storage.Transactions
{
    public interface ITransactionState
    {
        long Id { get; }
        ImmutableArray<IOperation> Operations { get; }
        TransactionStatus Status { get; }
        int Version { get; }
    }

    public interface IOperation
    {
        object Entry { get; }
        Type EntryType { get; }
        int? ExpectedVersion { get; }
        long Id { get; }
        OperationType OperationType { get; }
        OperationState State { get; }
        long TransactionId { get; }
    }
}