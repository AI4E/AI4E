using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public sealed class TransactionManager : ITransactionManager, IAsyncDisposable
    {
        private readonly ITransactionStateStorage _transactionStorage;
        private readonly ITransactionStateTransformer _transactionStateTransformer;
        private readonly IEntryStateTransformerFactory _entryStateTransformerFactory;
        private readonly IEntryStateStorageFactory _entryStorageFactory;
        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger<TransactionManager> _logger;
        private readonly AsyncProcess _transactionGarbageCollector;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        private readonly ConcurrentDictionary<Type, ITypedTransactionalManager> _typedManagers
            = new ConcurrentDictionary<Type, ITypedTransactionalManager>();

        private readonly WeakDictionary<long, ITransaction> _transactions = new WeakDictionary<long, ITransaction>();

        public TransactionManager(ITransactionStateStorage transactionStorage,
                                  ITransactionStateTransformer transactionStateTransformer,
                                  IEntryStateStorageFactory entryStorageFactory,
                                  IEntryStateTransformerFactory entryStateTransformerFactory,
                                  ILoggerFactory loggerFactory = null)
        {
            if (transactionStorage == null)
                throw new ArgumentNullException(nameof(transactionStorage));

            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            if (entryStateTransformerFactory == null)
                throw new ArgumentNullException(nameof(entryStateTransformerFactory));

            if (entryStorageFactory == null)
                throw new ArgumentNullException(nameof(entryStorageFactory));

            _transactionStorage = transactionStorage;
            _transactionStateTransformer = transactionStateTransformer;
            _entryStateTransformerFactory = entryStateTransformerFactory;
            _entryStorageFactory = entryStorageFactory;
            _loggerFactory = loggerFactory;

            _logger = loggerFactory?.CreateLogger<TransactionManager>();
            _transactionGarbageCollector = new AsyncProcess(GarbageCollectionProcess);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public ITransactionalDatabase CreateStore()
        {
            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            var logger = _loggerFactory?.CreateLogger<TransactionalDatabase>();

            return new TransactionalDatabase(this, _entryStateTransformerFactory, _entryStorageFactory, logger);
        }

        #region ProcessTransaction

        public async Task<ProcessingState> ProcessTransactionAsync(ITransaction transaction, CancellationToken cancellation)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            var processedTransactions = new HashSet<ITransaction>();
            var visitedTransactions = new HashSet<ITransaction>();

            var result = await ProcessTransactionAsync(originalTransaction: transaction,
                                                       transaction,
                                                       processedTransactions,
                                                       visitedTransactions,
                                                       cancellation);

#if DEBUG
            var state = await transaction.GetStateAsync(cancellation);

            Assert(!visitedTransactions.Any());
            Assert(state == TransactionStatus.Aborted ||
                   state == TransactionStatus.CleanedUp ||
                   state == null); // The garbage collector already collected the transaction
#endif


            return result;
        }

        private async Task<ProcessingState> ProcessTransactionAsync(ITransaction originalTransaction,
                                                                    ITransaction transaction,
                                                                    ISet<ITransaction> processedTransactions,
                                                                    ISet<ITransaction> visitedTransactions,
                                                                    CancellationToken cancellation)
        {
            Assert(transaction != null);
            Assert(processedTransactions != null);
            Assert(visitedTransactions != null);

            _logger?.LogTrace($"Processing transaction {transaction.Id}");

            var transactionState = await transaction.GetStateAsync(cancellation);
            var operations = await transaction.GetOperationsAsync(cancellation);

            if (transactionState.IsCommitted())
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {transaction.Id} committed successfully.");

                return ProcessingState.Committed;
            }

            if (transactionState == TransactionStatus.Aborted)
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {transaction.Id} aborted.");

                return ProcessingState.Aborted;
            }

            if (transactionState == TransactionStatus.AbortRequested)
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction abort requested. Aborting transaction {transaction.Id}");
                return await AbortTransactionAsync(originalTransaction, transaction, operations, cancellation);
            }

            if (visitedTransactions.Contains(transaction))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Detected cycle. Aborting transaction {transaction.Id}");
                return await AbortTransactionAsync(originalTransaction, transaction, operations, cancellation);
            }

            var dependencies = new HashSet<ITransaction>();
            if (!await ApplyOperationsAsync(transaction, operations, dependencies, cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction abort requested concurrently. Aborting transaction {transaction.Id}");
                return await AbortTransactionAsync(originalTransaction, transaction, operations, cancellation);
            }

            if (dependencies.Any())
            {
                var stringifiedDependencies = dependencies.Select(p => p.Id.ToString()).Aggregate((e, n) => e + ", " + n);

                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Processing dependencies of transaction {transaction.Id}: {stringifiedDependencies}.");
            }

            // Recursively process all dependencies.
            await ProcessDependenciesAsync(originalTransaction, transaction, dependencies, processedTransactions, visitedTransactions, cancellation);

#if DEBUG
            Assert(dependencies.All(p => processedTransactions.Contains(p)));

            foreach (var dependency in dependencies)
            {
                var state = await dependency.GetStateAsync(cancellation);

                Assert(state.IsCommitted() || state == TransactionStatus.Aborted);
            }

#endif

            if (!await CheckVersionsAsync(transaction, operations, cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction cannot be committed due to a version conflict. Aborting transaction {transaction.Id}");
                return await AbortTransactionAsync(originalTransaction, transaction, operations, cancellation);
            }

            if (!await transaction.TryCommitAsync(cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction abort requested concurrently. Aborting transaction {transaction.Id}");
                return await AbortTransactionAsync(originalTransaction, transaction, operations, cancellation);
            }

            _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {transaction.Id} committed successfully.");

            await CleanUpTransactionAsync(transaction, operations, cancellation);

            return ProcessingState.Committed;
        }

        private async Task<ProcessingState> AbortTransactionAsync(ITransaction originalTransaction,
                                                                  ITransaction transaction,
                                                                  ImmutableArray<IOperation> operations,
                                                                  CancellationToken cancellation)
        {
            if (!await transaction.TryRequestAbortAsync(cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {transaction.Id} committed successfully.");
                await CleanUpTransactionAsync(transaction, operations, cancellation);
                return ProcessingState.Committed;
            }

#if DEBUG
            var state = await transaction.GetStateAsync(cancellation);

            Assert(state == null || state == TransactionStatus.Aborted || state == TransactionStatus.AbortRequested);
#endif

            await AbortOperationsAsync(transaction, operations, cancellation);

            await transaction.AbortAsync(cancellation);

            _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {transaction.Id} aborted.");

            return ProcessingState.Aborted;
        }

        private async Task CleanUpTransactionAsync(ITransaction transaction, CancellationToken cancellation)
        {
            var operations = await transaction.GetOperationsAsync(cancellation);
            await CleanUpTransactionAsync(transaction, operations, cancellation);
        }

        private async Task CleanUpTransactionAsync(ITransaction transaction, ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
            await CommitOperationsAsync(transaction, operations, cancellation);
            await transaction.CleanUp(cancellation);
        }

        private async Task ProcessDependenciesAsync(ITransaction originalTransaction,
                                                    ITransaction transaction,
                                                    ISet<ITransaction> dependencies,
                                                    ISet<ITransaction> processedTransactions,
                                                    ISet<ITransaction> visitedTransactions,
                                                    CancellationToken cancellation)
        {
            if (!dependencies.Any())
            {
                return;
            }

            visitedTransactions.Add(transaction);
            foreach (var dependency in dependencies)
            {
                if (processedTransactions.Contains(dependency))
                    continue;

                await ProcessTransactionAsync(originalTransaction, dependency, processedTransactions, visitedTransactions, cancellation);

#if DEBUG

                var state = await dependency.GetStateAsync(cancellation);

                Assert(state == TransactionStatus.Aborted || state.IsCommitted());

#endif

                processedTransactions.Add(dependency);
            }
            var removed = visitedTransactions.Remove(transaction);

            Assert(removed);
        }

        // True => Commit ; False => Abort
        private async Task<bool> CheckVersionsAsync(ITransaction transaction, ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
#if DEBUG
            var notFound = false;
#endif
            foreach (var operation in operations)
            {
                var expectedVersion = operation.ExpectedVersion;

                if (expectedVersion == null)
                {
                    continue;
                }

                var typedStore = GetTypedManager(operation.EntryType);
                var originalEntryVersion = await typedStore.GetOriginalVersionAsync(operation.Entry, transaction, cancellation);

                if (originalEntryVersion == null)
                {
#if DEBUG
                    notFound = true;
#endif

                    break;
                }

                if (expectedVersion != originalEntryVersion)
                {
                    return false;
                }
            }

            var state = await transaction.GetStateAsync(cancellation);

#if DEBUG
            if (notFound)
            {
                Assert(state != TransactionStatus.Pending);
            }
#endif

            return state.IsCommitted() || state == TransactionStatus.Pending;
        }

        // True => Commit ; False => Abort
        private async Task<bool> ApplyOperationsAsync(ITransaction transaction, ImmutableArray<IOperation> operations, ISet<ITransaction> dependencies, CancellationToken cancellation)
        {
            Assert(transaction != null);
            Assert(operations != null);
            Assert(dependencies != null);

            foreach (var operation in operations.Shuffle())
            {
                var typedStore = GetTypedManager(operation.EntryType);
                var isApplied = operation.State == OperationState.Applied;

                // Apply each operation to the respective entry our load the dependencies if the operation is already applied.
                var dependentTransactions = isApplied ?
                                            await typedStore.GetDependenciesAsync(operation.Entry, transaction, cancellation) :
                                            await typedStore.ApplyOperationAsync(operation.Entry, operation.OperationType, transaction, cancellation);

                // If we are in the state CleanedUp, dependentTransactions must be null because 
                // 1) GetDependencies cannot find a pending operation for our transaction which is the precondition to get to state CleanedUp
                // 2) ApplyOperation cannot apply a pending operation for our transaction 
                //    because it is not present in the pending transactions collection which is the precondition to get to state CleanedUp

                if (dependentTransactions == null)
                {
                    var state = await transaction.GetStateAsync(cancellation);

                    // If we got here, we cannot be in state pending any more because
                    // 1) GetDependencies does not find a pending operation for our transaction. 
                    //    This can only accour if the pending operation was removed. 
                    //    To be able to remove a pending operation, the respective transaction must either be in state AbortRequested or Committed.
                    // 2) ApplyOperation cannot apply a pending operation for our transaction 
                    //    because it is not present in the pending transactions collection.
                    //    To remote the transaction from the pending transactions collection, the respective pending operation must be removed from the entry
                    //    To be able to remove a pending operation, the respective transaction must either be in state AbortRequested or Committed.

                    Assert(state != TransactionStatus.Pending);

                    return state.IsCommitted();
                }

                if (dependentTransactions.Any())
                {
                    dependencies.UnionWith(dependentTransactions);
                }

                if (!isApplied && !await transaction.TryApplyOperationAsync(operation, cancellation))
                {
                    await typedStore.AbortOperationAsync(operation.Entry, transaction, cancellation);

                    return false;
                }
            }

            return true;
        }

        private async Task AbortOperationsAsync(ITransaction transaction, ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
            Assert(transaction != null);
            Assert(operations != null);

            foreach (var operation in operations.Shuffle())
            {
                // Cannot be done as it must be ensured that transaction is removes from the pending transactions collection.
                //if (operation.State == OperationState.Unapplied)
                //    continue;

                // Unapply each operation from the respective entry
                var typedStore = GetTypedManager(operation.EntryType);

                await typedStore.AbortOperationAsync(operation.Entry, transaction, cancellation);
                if (!await transaction.TryUnapplyOperationAsync(operation, cancellation))
                {
                    // The transaction aborted concurrently.

                    break;
                }
            }
        }

        private async Task CommitOperationsAsync(ITransaction transaction, ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
            Assert(transaction != null);
            Assert(operations != null);

            foreach (var operation in operations.Shuffle())
            {
                // Commit each operation to the respective entry
                var typedStore = GetTypedManager(operation.EntryType);

                await typedStore.CommitOperationAsync(operation.Entry, transaction, cancellation);
            }
        }

        #endregion

        #region GarbageCollection

        private async Task GarbageCollectionProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    //_logger?.LogDebug("Performing transaction garbage collection.");

                    //// TODO: We can also include pending operations if we store a time stamp on the transaction that the respecting transaction was touched last.
                    ////       We can compare this time stamp with the current time and define a limit that must be reached in order to process the transaction.

                    //var entries = await _transactionStorage.GetTransactionsAsync(p => p.State == TransactionState.Committed ||
                    //                                                                  p.State == TransactionState.CleanedUp ||
                    //                                                                  p.State == TransactionState.AbortRequested ||
                    //                                                                  p.State == TransactionState.Aborted);

                    //var transactions = entries.Select(entry => (transaction: GetTransaction(entry), state: entry.State));

                    //var largestId = 0L;

                    //// TODO: Parallelize this
                    //foreach (var (transaction, state) in transactions)
                    //{
                    //    if (state == TransactionState.Committed)
                    //    {
                    //        await CleanUpTransactionAsync(transaction, cancellation);
                    //    }
                    //    else if (state == TransactionState.AbortRequested || state == TransactionState.Pending)
                    //    {
                    //        await ProcessTransactionAsync(GetTransaction(transaction.Id), cancellation);
                    //    }

                    //    if (transaction.Id > largestId)
                    //    {
                    //        largestId = transaction.Id;
                    //    }
                    //}

                    //foreach (var (transaction, _) in transactions)
                    //{
                    //    Assert(largestId != 0);

                    //    // We must ensure that the transaction with the largest id stays present in the database in order that the transaction id sequence is in order.
                    //    if (transaction.Id == largestId)
                    //    {
                    //        continue;
                    //    }

                    //    await transaction.DeleteTransactionAsync(cancellation);
                    //}

                    await Task.Delay(/*TimeSpan.FromSeconds(10)*/-1, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, "Unexpected exception while collecting transactions.");
                }
            }
        }

        #endregion

        #region Initialization

        private Task InitializeInternalAsync(CancellationToken cancellation)
        {
            return _transactionGarbageCollector.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);

            await _transactionGarbageCollector.TerminateAsync().HandleExceptionsAsync(_logger);
        }

        #endregion

        #region TypedManager

        private ITypedTransactionalManager GetTypedManager(Type dataType)
        {
            return _typedManagers.GetOrAdd(dataType, CreateTypedManager);
        }

        private ITypedTransactionalManager CreateTypedManager(Type dataType)
        {
            Assert(dataType != null);

            var idType = DataPropertyHelper.GetIdType(dataType);

            var typedStore = Activator.CreateInstance(typeof(TypedTransactionalManager<,>).MakeGenericType(idType, dataType), this);

            return (ITypedTransactionalManager)typedStore;
        }

        #endregion

        #region Transaction

        public ITransaction CreateTransaction()
        {
            return new Transaction(_transactionStorage, _transactionStateTransformer);
        }

        public async Task<IEnumerable<ITransaction>> GetNonCommittedTransactionsAsync(CancellationToken cancellation = default)
        {
            var transactions = await _transactionStorage.GetNonCommittedTransactionsAsync(cancellation);

            return transactions.Select(p => GetTransaction(p));
        }

        private ITransaction GetTransaction(ITransactionState entry)
        {
            Assert(entry != null);

            return _transactions.GetOrAdd(entry.Id, _ => BuildTransaction(entry));
        }

        public ITransaction GetTransaction(long id)
        {
            return _transactions.GetOrAdd(id, BuildTransaction);
        }

        private ITransaction BuildTransaction(long id)
        {
            return new Transaction(id, _transactionStorage, _transactionStateTransformer);
        }

        private ITransaction BuildTransaction(ITransactionState entry)
        {
            return new Transaction(entry, _transactionStorage, _transactionStateTransformer);
        }

        #endregion

        private interface ITypedTransactionalManager
        {
            Task<IEnumerable<ITransaction>> ApplyOperationAsync(object data, OperationType operationType, ITransaction transaction, CancellationToken cancellation);
            Task<IEnumerable<ITransaction>> GetDependenciesAsync(object data, ITransaction transaction, CancellationToken cancellation);
            Task AbortOperationAsync(object data, ITransaction transaction, CancellationToken cancellation);
            Task CommitOperationAsync(object data, ITransaction transaction, CancellationToken cancellation);
            Task<int?> GetOriginalVersionAsync(object data, ITransaction transaction, CancellationToken cancellation);
        }

        private sealed class TypedTransactionalManager<TId, TData> : ITypedTransactionalManager
            where TData : class
        {
            private readonly TransactionManager _transactionalManager;
            private readonly ITransactionStateStorage _transactionStorage;
            private readonly IEntryStateStorage<TId, TData> _entryStorage;
            private readonly IEntryStateTransformer<TId, TData> _entryStateTransformer;
            private readonly ILogger<TransactionManager> _logger;

            public TypedTransactionalManager(TransactionManager transactionalManager)
            {
                Assert(transactionalManager != null);

                _transactionalManager = transactionalManager;

                _transactionStorage = _transactionalManager._transactionStorage;
                _entryStateTransformer = _transactionalManager._entryStateTransformerFactory.GetEntryManager<TId, TData>();
                _entryStorage = _transactionalManager._entryStorageFactory.GetEntryStorage<TId, TData>();
                _logger = _transactionalManager._logger;

                Assert(_transactionStorage != null);
                Assert(_entryStateTransformer != null);
                Assert(_entryStorage != null);
            }

            public Task<IEnumerable<ITransaction>> GetDependenciesAsync(object data, ITransaction transaction, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return GetDependenciesAsync(typedData, transaction, cancellation);
            }

            private async Task<IEnumerable<ITransaction>> GetDependenciesAsync(TData data, ITransaction transaction, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entry = await _entryStorage.GetEntryAsync(DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id), cancellation);
                ISet<ITransaction> dependencies = new HashSet<ITransaction>();

                if (entry == null)
                {
                    return Enumerable.Empty<ITransaction>();
                }

                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    var pendingOperation = entry.PendingOperations[i];

                    if (pendingOperation.TransactionId == transaction.Id)
                    {
                        return dependencies;
                    }

                    var dependency = _transactionalManager.GetTransaction(pendingOperation.TransactionId);

                    dependencies.Add(dependency);
                }

                return null;
            }

            public Task<int?> GetOriginalVersionAsync(object data, ITransaction transaction, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return GetOriginalVersionAsync(typedData, transaction, cancellation);
            }

            private async Task<int?> GetOriginalVersionAsync(TData data, ITransaction transaction, CancellationToken cancellation)
            {
                if (transaction == null)
                    throw new ArgumentNullException(nameof(transaction));

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entry = await _entryStorage.GetEntryAsync(DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id), cancellation);
                Assert(entry != null);

                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    var pendingOperation = entry.PendingOperations[i];

                    if (pendingOperation.TransactionId == transaction.Id)
                    {
                        return pendingOperation.OriginalData.DataVersion;
                    }

#if DEBUG
                    var tx = _transactionalManager.GetTransaction(pendingOperation.TransactionId);

                    var txState = await tx.GetStateAsync(cancellation);

                    if (txState != TransactionStatus.Committed)
                    {
                        var transactionState = await transaction.GetStateAsync(cancellation);

                        Assert(transactionState == TransactionStatus.Aborted ||
                               transactionState == TransactionStatus.AbortRequested ||
                               transactionState.IsCommitted());
                    }
#endif
                }

                return null;
            }

            public Task<IEnumerable<ITransaction>> ApplyOperationAsync(object data, OperationType operationType, ITransaction transaction, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return ApplyOperationAsync(typedData, operationType, transaction, cancellation);
            }

            private async Task<IEnumerable<ITransaction>> ApplyOperationAsync(TData data, OperationType operationType, ITransaction transaction, CancellationToken cancellation)
            {
                if (transaction == null)
                    throw new ArgumentNullException(nameof(transaction));

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                IEntryState<TId, TData> entry, desired = null;
                ISet<ITransaction> dependencies;

                do
                {
                    dependencies = new HashSet<ITransaction>();
                    entry = await _entryStorage.GetEntryAsync(DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id), cancellation);

                    if (entry == null)
                    {
                        if (operationType == OperationType.Delete)
                        {
                            // TODO: Do we need to store an operation actually?
                            break;
                        }

                        if (operationType == OperationType.Store)
                        {
                            desired = _entryStateTransformer.Create(id, data, transaction.Id);
                        }
                    }
                    else
                    {
                        if (!entry.PendingTransactions.Contains(transaction.Id))
                        {
                            return null;
                        }

                        for (var i = 0; i < entry.PendingOperations.Count; i++)
                        {
                            var pendingOperation = entry.PendingOperations[i];

                            if (pendingOperation.TransactionId == transaction.Id)
                            {
                                return dependencies;
                            }

                            var dependency = _transactionalManager.GetTransaction(pendingOperation.TransactionId);

                            dependencies.Add(dependency);
                        }

                        var deletion = operationType == OperationType.Delete;

                        desired = _entryStateTransformer.Store(entry, deletion ? null : data, transaction.Id);
                    }

                    Assert(desired != null);
                }
                while (!await _entryStorage.CompareExchangeAsync(desired, entry, cancellation));

                _logger?.LogTrace($"Applied operation for transaction {transaction.Id} to entry {id}.");

                Assert(desired.PendingOperations.Last().TransactionId == transaction.Id);

                return dependencies;
            }

            public Task AbortOperationAsync(object data, ITransaction transaction, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return AbortOperationAsync(typedData, transaction, cancellation);
            }

            private async Task AbortOperationAsync(TData data, ITransaction transaction, CancellationToken cancellation)
            {
                if (transaction == null)
                    throw new ArgumentNullException(nameof(transaction));

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var predicate = DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id);

                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.Abort(c, transaction.Id);
                }

                var entry = await _entryStorage.UpdateEntryAsync(predicate, Update, cancellation);

                _logger?.LogTrace($"Aborted operation for transaction {transaction.Id} to entry {id}.");

                Assert(!entry.PendingOperations.Any(p => p.TransactionId == transaction.Id));
            }

            public Task CommitOperationAsync(object data, ITransaction transaction, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return CommitOperationAsync(typedData, transaction, cancellation);
            }

            private async Task CommitOperationAsync(TData data, ITransaction transaction, CancellationToken cancellation)
            {
                if (transaction == null)
                    throw new ArgumentNullException(nameof(transaction));

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var predicate = DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id);

                IEntryState<TId, TData> Update(IEntryState<TId, TData> current)
                {
                    return _entryStateTransformer.Commit(current, transaction.Id);
                }

                await _entryStorage.UpdateEntryAsync(predicate, Update, cancellation);

                _logger?.LogTrace($"Committed operation for transaction {transaction.Id} to entry {id}.");
            }
        }
    }
}
