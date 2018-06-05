using System;
using System.Collections.Immutable;

namespace AI4E.Storage.Transactions
{
    public interface IEntryState<TId, TData> : IDataRepresentation<TId, TData>
        where TData : class
    {
        int DataVersion { get; }
        DateTime LastWriteTime { get; }

        int Version { get; }
        DateTime CreationTime { get; }
        long CreatingTransaction { get; }
        ImmutableList<IPendingOperation<TId, TData>> PendingOperations { get; }
        ImmutableList<long> PendingTransactions { get; }
    }

    public interface IEntrySnapshot<TId, TData> : IDataRepresentation<TId, TData>
        where TData : class
    {
        int DataVersion { get; }
        DateTime? LastWriteTime { get; }
    }

    public interface IDataRepresentation<TId, TData>
        where TData : class
    {
        TId Id { get; }
        TData Data { get; }
    }

    public interface IPendingOperation<TId, TData>
        where TData : class
    {
        long TransactionId { get; }
        IEntrySnapshot<TId, TData> OriginalData { get; }
        DateTime OperationTime { get; }
    }
}
