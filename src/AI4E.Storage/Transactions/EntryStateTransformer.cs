using System;
using System.Collections.Immutable;
using System.Linq;
using static System.Diagnostics.Debug;
using static AI4E.Utils.DebugEx;

namespace AI4E.Storage.Transactions
{
    public sealed class EntryStateTransformer<TId, TData> : IEntryStateTransformer<TId, TData>
        where TData : class
    {
        private static readonly ImmutableList<IPendingOperation<TId, TData>> _noPendingOperations
            = ImmutableList<IPendingOperation<TId, TData>>.Empty;

        private static readonly ImmutableList<long> _noPendingTransactions
            = ImmutableList<long>.Empty;

        private readonly IDateTimeProvider _dateTimeProvider;

        public EntryStateTransformer(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        public IEntryState<TId, TData> Create(TId id, TData data, long transactionId)
        {
            var creationTime = _dateTimeProvider.GetCurrentTime();
            var pendingOperation = new PendingOperation(transactionId,
                                                        originalData: new EntrySnapshot(id, null, 0, null),
                                                        operationTime: creationTime);
            var pendingOperations = _noPendingOperations.Add(pendingOperation);
            var pendingTransactions = _noPendingTransactions.Add(transactionId);

            return new Entry(id,
                             data,
                             dataVersion: 1,
                             version: 1,
                             creationTransaction: transactionId,
                             pendingOperations,
                             pendingTransactions,
                             creationTime,
                             lastWriteTime:
                             creationTime);
        }

        public IEntryState<TId, TData> AddPendingTransaction(IEntryState<TId, TData> entry, long transactionId)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (entry.PendingTransactions.Contains(transactionId))
            {
                return entry;
            }

            var lastWriteTime = _dateTimeProvider.GetCurrentTime();
            var pendingTransactions = entry.PendingTransactions.Add(transactionId);

            return new Entry(entry.Id,
                             entry.Data,
                             entry.DataVersion,
                             entry.Version + 1,
                             entry.CreatingTransaction,
                             entry.PendingOperations,
                             pendingTransactions,
                             entry.CreationTime,
                             lastWriteTime);
        }

        public IEntryState<TId, TData> Store(IEntryState<TId, TData> entry, TData data, long transactionId)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!entry.PendingTransactions.Contains(transactionId))
                throw new InvalidOperationException();

            if (entry.PendingOperations.Any(p => p.TransactionId == transactionId))
                throw new InvalidOperationException("A single transaction must modify an entry with a single operation only.");

            var lastWriteTime = _dateTimeProvider.GetCurrentTime();
            var pendingOperation = new PendingOperation(transactionId, originalData: ToSnapshot(entry), lastWriteTime);
            var pendingOperations = entry.PendingOperations.Add(pendingOperation);

            return new Entry(entry.Id,
                             data,
                             entry.DataVersion + 1,
                             entry.Version + 1,
                             entry.CreatingTransaction,
                             pendingOperations,
                             entry.PendingTransactions,
                             entry.CreationTime,
                             lastWriteTime);
        }

        public IEntryState<TId, TData> Abort(IEntryState<TId, TData> entry, long transactionId)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var pendingTransactions = entry.PendingTransactions.Remove(transactionId);

            var i = entry.PendingOperations.FindIndex(p => p.TransactionId == transactionId);

            // We abort the operation that happend last
            if (i >= 0 && i == entry.PendingOperations.Count() - 1)
            {
                var originalData = entry.PendingOperations[i].OriginalData;

                entry = new Entry(entry.Id,
                                  originalData.Data,
                                  originalData.DataVersion,
                                  entry.Version + 1,
                                  entry.CreatingTransaction,
                                  entry.PendingOperations.RemoveAt(entry.PendingOperations.Count - 1),
                                  pendingTransactions,
                                  entry.CreationTime,
                                  originalData.LastWriteTime ?? entry.CreationTime);
            }
            else if (i >= 0)
            {
#if DEBUG
                Assert(entry.PendingOperations.Count(p => p.TransactionId == transactionId) == 1);
#endif

                var pendingOperations = entry.PendingOperations;
                var originalData = pendingOperations[i].OriginalData;
                pendingOperations = pendingOperations.RemoveAt(i);

                // The operations after the removed one now have an index that is one smaller
                var operationToReplace = pendingOperations[i]; // index i + 1 is now index i
                var replacement = new PendingOperation(operationToReplace.TransactionId, originalData, operationToReplace.OperationTime);
                pendingOperations = pendingOperations.Replace(operationToReplace, replacement);

                entry = new Entry(entry.Id,
                                  entry.Data,
                                  entry.DataVersion,
                                  entry.Version + 1,
                                  entry.CreatingTransaction,
                                  pendingOperations,
                                  pendingTransactions,
                                  entry.CreationTime,
                                  entry.LastWriteTime);

            }
            else if (entry.PendingTransactions.Contains(transactionId))
            {
                entry = new Entry(entry.Id,
                                  entry.Data,
                                  entry.DataVersion,
                                  entry.Version + 1,
                                  entry.CreatingTransaction,
                                  entry.PendingOperations,
                                  pendingTransactions,
                                  entry.CreationTime,
                                  entry.LastWriteTime);
            }

            Assert(!entry.PendingOperations.Any(p => p.TransactionId == transactionId));

            return entry;
        }

        public IEntryState<TId, TData> Commit(IEntryState<TId, TData> entry, long transactionId)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var lastWriteTime = _dateTimeProvider.GetCurrentTime();

            var pendingOperations = entry.PendingOperations.RemoveAll(op => op.TransactionId == transactionId);
            var pendingTransactions = entry.PendingTransactions.Remove(transactionId);

            entry = new Entry(entry.Id,
                             entry.Data,
                             entry.DataVersion,
                             entry.Version + 1,
                             entry.CreatingTransaction,
                             pendingOperations,
                             pendingTransactions,
                             entry.CreationTime,
                             lastWriteTime);

            Assert(!entry.PendingOperations.Any(p => p.TransactionId == transactionId));

            return entry;
        }

        public IEntrySnapshot<TId, TData> ToSnapshot(IEntryState<TId, TData> entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return new EntrySnapshot(entry.Id, entry.Data, entry.DataVersion, entry.LastWriteTime);
        }

        private sealed class Entry : IEntryState<TId, TData>
        {
            public Entry(IEntryState<TId, TData> entry)
            {
                Assert(entry.Data != null);
                Assert(entry.PendingOperations != null);
                Assert(entry.CreationTime <= entry.LastWriteTime);

                Id = entry.Id;
                Data = entry.Data;
                DataVersion = entry.DataVersion;
                Version = entry.Version;
                CreatingTransaction = entry.CreatingTransaction;
                PendingOperations = entry.PendingOperations;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                PendingTransactions = entry.PendingTransactions;
            }

            public Entry(TId id,
                         TData data,
                         int dataVersion,
                         int version,
                         long creationTransaction,
                         ImmutableList<IPendingOperation<TId, TData>> pendingOperations,
                         ImmutableList<long> pendingTransactions,
                         DateTime creationTime,
                         DateTime lastWriteTime)
            {
                Assert(data != null, dataVersion > 0);

                Assert(pendingOperations != null);
                Assert(creationTime <= lastWriteTime);

                Id = id;
                Data = data;
                DataVersion = dataVersion;
                Version = version;
                CreatingTransaction = creationTransaction;
                PendingTransactions = pendingTransactions;
                PendingOperations = pendingOperations;
                CreationTime = creationTime;
                LastWriteTime = lastWriteTime;
            }

            public TId Id { get; }

            public TData Data { get; }

            public int DataVersion { get; }

            public int Version { get; }

            public long CreatingTransaction { get; }

            public ImmutableList<IPendingOperation<TId, TData>> PendingOperations { get; }

            public ImmutableList<long> PendingTransactions { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }
        }

        private sealed class PendingOperation : IPendingOperation<TId, TData>
        {
            public PendingOperation(long transactionId,
                                    IEntrySnapshot<TId, TData> originalData,
                                    DateTime operationTime)
            {
                Assert(originalData != null);

                TransactionId = transactionId;
                OriginalData = originalData;
                OperationTime = operationTime;
            }

            public long TransactionId { get; }
            public IEntrySnapshot<TId, TData> OriginalData { get; }
            public DateTime OperationTime { get; }
        }

        private sealed class EntrySnapshot : IEntrySnapshot<TId, TData>
        {
            public EntrySnapshot(TId id, TData data, int dataVersion, DateTime? lastWriteTime)
            {
                Assert(data != null, dataVersion > 0);
                Assert(dataVersion == 0, data == null && lastWriteTime == null);

                Id = id;
                Data = data;
                DataVersion = dataVersion;
                LastWriteTime = lastWriteTime;
            }

            public TId Id { get; }
            public TData Data { get; }
            public int DataVersion { get; }
            public DateTime? LastWriteTime { get; }
        }
    }

    public sealed class EntryStateTransformerFactory : IEntryStateTransformerFactory
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public EntryStateTransformerFactory(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        public IEntryStateTransformer<TId, TData> GetEntryManager<TId, TData>()
            where TData : class
        {
            return new EntryStateTransformer<TId, TData>(_dateTimeProvider);
        }
    }
}
