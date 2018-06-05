using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public sealed class TransactionStateStorage : ITransactionStateStorage
    {
        private readonly IFilterableDatabase _database;

        public TransactionStateStorage(IFilterableDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        public ValueTask<bool> CompareExchangeAsync(ITransactionState transaction,
                                                    ITransactionState comparand,
                                                    CancellationToken cancellation = default)
        {
            var data = AsStoredTransaction(transaction);
            var cmpr = AsStoredTransaction(comparand);

            return _database.CompareExchangeAsync(data, cmpr, p => p.Version, cancellation);
        }

        public Task RemoveAsync(ITransactionState transaction, CancellationToken cancellation = default)
        {
            var data = AsStoredTransaction(transaction);
            return _database.RemoveAsync(data, cancellation);
        }

        public async ValueTask<ITransactionState> GetLatestTransactionAsync(long minId = default, CancellationToken cancellation = default)
        {
            StoredTransaction transaction;

            if (minId == default)
            {
                transaction = await _database.GetOneAsync<StoredTransaction>(cancellation);
            }
            else
            {
                transaction = await _database.GetOneAsync<StoredTransaction>(p => p.Id >= minId, cancellation);
            }

            return transaction;
        }

        public async ValueTask<ITransactionState> GetTransactionAsync(long id, CancellationToken cancellation = default)
        {
            return await _database.GetOneAsync<StoredTransaction>(p => p.Id == id, cancellation);
        }

        // TODO: Return async enumerable
        public async ValueTask<IEnumerable<ITransactionState>> GetNonCommittedTransactionsAsync(CancellationToken cancellation = default)
        {
            return await _database.GetAsync<StoredTransaction>(p => p.Status == TransactionStatus.AbortRequested || p.Status == TransactionStatus.Pending, cancellation);
        }

        private static StoredTransaction AsStoredTransaction(ITransactionState transaction)
        {
            if (transaction == null)
                return null;

            if (transaction is StoredTransaction result)
                return result;

            return new StoredTransaction(transaction);
        }


        private static StoredOperation AsStoredOperation(IOperation operation)
        {
            if (operation == null)
                return null;

            if (operation is StoredOperation result)
                return result;

            return new StoredOperation(operation);
        }

        private sealed class StoredTransaction : ITransactionState
        {
            public StoredTransaction()
            {
                Operations = new List<StoredOperation>();
            }

            public StoredTransaction(ITransactionState transaction)
            {
                Assert(transaction != null);

                Id = transaction.Id;
                Operations = new List<StoredOperation>(transaction.Operations.Select(p => AsStoredOperation(p)));
                Status = transaction.Status;
                Version = transaction.Version;
            }

            public long Id { get; private set; }

            public List<StoredOperation> Operations { get; private set; }

            public TransactionStatus Status { get; private set; }

            public int Version { get; private set; }

            ImmutableArray<IOperation> ITransactionState.Operations => ImmutableArray<IOperation>.CastUp(Operations.ToImmutableArray());
        }

        private sealed class StoredOperation : IOperation
        {
            public StoredOperation() { }

            public StoredOperation(IOperation operation)
            {
                Assert(operation != null);

                Id = operation.Id;
                Entry = operation.Entry;
                EntryType = operation.EntryType.AssemblyQualifiedName;
                ExpectedVersion = operation.ExpectedVersion;
                OperationType = operation.OperationType;
                State = operation.State;
                TransactionId = operation.TransactionId;
            }

            public object Entry { get; private set; }

            public string EntryType { get; private set; }

            public int? ExpectedVersion { get; private set; }

            public OperationType OperationType { get; private set; }

            public OperationState State { get; private set; }

            public long Id { get; private set; }

            public long TransactionId { get; private set; }

            Type IOperation.EntryType => LoadTypeIgnoringVersion(EntryType);

            private static Type LoadTypeIgnoringVersion(string assemblyQualifiedName)
            {
                return Type.GetType(assemblyQualifiedName, assemblyName => { assemblyName.Version = null; return Assembly.Load(assemblyName); }, null);
            }
        }
    }
}

