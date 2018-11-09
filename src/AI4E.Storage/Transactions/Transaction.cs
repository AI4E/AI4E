using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public sealed partial class Transaction : IEquatable<Transaction>, ITransaction
    {
        #region Fields

        private readonly ITransactionStateStorage _transactionStorage;
        private readonly ITransactionStateTransformer _transactionStateTransformer;
        private readonly IEntryStateStorageFactory _entryStateStorageFactory;
        private readonly IEntryStateTransformerFactory _entryStateTransformerFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<Transaction> _logger;
        private readonly TransactionManager _transactionManager;

        private ITransactionState _entry;
        private int _entryVersion;
        private readonly object _entryLock = new object();

        private readonly ConcurrentDictionary<Type, ITypedTransaction> _typedTransactions = new ConcurrentDictionary<Type, ITypedTransaction>();

        // This is null for transactions that we do not own to prevent bugs.
        private readonly ISet<Transaction> _nonCommittedTransactions;
        private readonly AsyncLock _lock;

        #endregion

        #region C'tor

        internal Transaction(long id,
                              bool ownsTransaction,
                              ITransactionState entry,
                              TransactionManager transactionManager,
                              ITransactionStateStorage transactionStorage,
                              ITransactionStateTransformer transactionStateTransformer,
                              IEntryStateStorageFactory entryStateStorageFactory,
                              IEntryStateTransformerFactory entryStateTransformerFactory,
                              ILoggerFactory loggerFactory = null)
        {

            _transactionManager = transactionManager;
            _transactionStorage = transactionStorage;
            _transactionStateTransformer = transactionStateTransformer;
            _entryStateStorageFactory = entryStateStorageFactory;
            _entryStateTransformerFactory = entryStateTransformerFactory;
            _loggerFactory = loggerFactory;

            _logger = _loggerFactory?.CreateLogger<Transaction>();

            Id = id;
            OwnsTransaction = ownsTransaction;
            _entry = entry;
            _entryVersion = entry?.Version ?? 0;

            if (OwnsTransaction)
            {
                _nonCommittedTransactions = new HashSet<Transaction>();
                _lock = new AsyncLock();
            }
        }

        #endregion

        #region Properties

        public long Id { get; }
        public bool OwnsTransaction { get; }

        public ITransactionState UnderlyingState
        {
            get
            {
                lock (_entryLock)
                    return _entry;
            }
        }

        // This does NOT update the internal state, as was done previously. Call UpdateAsync before the access explicitely, if this behavior is needed.
        public TransactionStatus? Status => UnderlyingState?.Status;

        // This does NOT update the internal state, as was done previously. Call UpdateAsync before the access explicitely, if this behavior is needed.
        public ImmutableArray<IOperation> Operations => UnderlyingState?.Operations ?? ImmutableArray<IOperation>.Empty;

        #endregion

        #region Lifetime state transitions

        // Transitions the transaction to the "Prepare" lifetime state
        // This can only be done, if we own the transaction.
        private async Task<bool> TryTransitionPrepareStateAsync(CancellationToken cancellation)
        {
            Assert(OwnsTransaction);

            bool Condition(ITransactionState current)
            {
                return current.Status == TransactionStatus.Initial || current.Status == TransactionStatus.Prepare;
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Prepare(current), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return entry != null && success;
        }

        // Transitions the transaction to the "Pending" lifetime state
        // This can only be done, if we own the transaction.
        private async Task<bool> TryTransitionPendingStateAsync(CancellationToken cancellation)
        {
            Assert(OwnsTransaction);

            bool Condition(ITransactionState current)
            {
                return current.Status != TransactionStatus.Aborted && // TODO: Is this correct?
                       current.Status != TransactionStatus.AbortRequested;
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.BeginCommit(current), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return entry != null && success;
        }

        private async Task<bool> TryTransitionCommittedStateAsync(CancellationToken cancellation)
        {
            bool Condition(ITransactionState current)
            {
                return current.Status != TransactionStatus.Aborted && // TODO: Is this correct?
                       current.Status != TransactionStatus.AbortRequested &&
                       current.Operations.All(p => p.State == OperationState.Applied);
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Commit(current), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return entry != null && (success || entry.Status.IsCommitted());
        }

        private async Task<bool> TryTransitionRequestAbortedStateAsync(CancellationToken cancellation)
        {
            bool Condition(ITransactionState current)
            {
                return !current.Status.IsCommitted();
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.RequestAbort(current), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return entry == null || success;
        }

        private async Task TransitionAbortStateAsync(CancellationToken cancellation)
        {
            ITransactionState Update(ITransactionState current)
            {
                return _transactionStateTransformer.Abort(current);
            }

            await UpdateEntry(Update, cancellation);
        }

        private async Task TransitionCleanUpStateAsync(CancellationToken cancellation)
        {
            ITransactionState Update(ITransactionState current)
            {
                return _transactionStateTransformer.CleanUp(current);
            }

            await UpdateEntry(Update, cancellation);
        }

        private Task<(ITransactionState entry, T result)> UpdateEntry<T>(Func<ITransactionState, (ITransactionState entry, T result)> update,
                                                                        CancellationToken cancellation)
        {
            return UpdateEntry(update, condition: entry => true, cancellation);
        }

        private async Task<(ITransactionState entry, T result)> UpdateEntry<T>(Func<ITransactionState, (ITransactionState entry, T result)> update,
                                                                               Func<ITransactionState, bool> condition,
                                                                               CancellationToken cancellation)
        {
            Assert(update != null);
            Assert(condition != null);

            var entry = UnderlyingState ?? await UpdateAsync(cancellation);

            if (entry == null)
            {
                return default;
            }

            ITransactionState desired;
            T result;
            T ret = default;

            while (condition(entry))
            {
                (desired, result) = update(entry);

                if (desired == entry)
                {
                    ret = result;
                    break;
                }

                if (await _transactionStorage.CompareExchangeAsync(desired, entry, cancellation))
                {
                    entry = desired;
                    ret = result;
                    break;
                }

                entry = await UpdateAsync(cancellation);

                if (entry == null)
                {
                    return (null, default);
                }
            }

            Assert(entry != null);

            Update(entry);

            return (entry, ret);
        }

        #endregion

        #region Operations

        private async Task<IOperation> StoreOperationAsync<TData>(TData data, int? expectedVersion, CancellationToken cancellation)
        {
            Assert(OwnsTransaction);

            (ITransactionState, IOperation) Update(ITransactionState current)
            {
                var entryType = typeof(TData);
                var predicate = DataPropertyHelper.CompilePredicate(entryType, data);
                var operationToReplace = current.Operations.FirstOrDefault(p => p.EntryType == entryType && predicate(p.Entry));
                if (operationToReplace != null)
                {
                    current = _transactionStateTransformer.RemoveOperation(current, operationToReplace);
                }
                var desired = _transactionStateTransformer.Store(current, data, expectedVersion, out var operation);
                return (desired, operation);
            }

            if (Status != TransactionStatus.Initial)
            {
                ThrowInvalidOpRecordingOperationAfterStart();
            }

            var (entry, result) = await UpdateEntry(Update, cancellation);

            if (entry == null)
            {
                ThrowInvalidOpRecordingOperationAfterStart();
            }

            Assert(result != null);

            return result;
        }

        // TODO: If we delete a non existing entry, or creating an entry and delete it afterwards, this should be a no op.
        private async Task<IOperation> DeleteOperationAsync<TData>(TData data, int? expectedVersion, CancellationToken cancellation)
        {
            Assert(OwnsTransaction);

            (ITransactionState, IOperation) Update(ITransactionState current)
            {
                var entryType = typeof(TData);
                var predicate = DataPropertyHelper.CompilePredicate(entryType, data);
                var operationToReplace = current.Operations.FirstOrDefault(p => p.EntryType == entryType && predicate(p.Entry));
                if (operationToReplace != null)
                {
                    current = _transactionStateTransformer.RemoveOperation(current, operationToReplace);
                }
                var desired = _transactionStateTransformer.Delete(current, data, expectedVersion, out var operation);
                return (desired, operation);
            }

            if (Status != TransactionStatus.Initial)
            {
                ThrowInvalidOpRecordingOperationAfterStart();
            }

            var (entry, result) = await UpdateEntry(Update, cancellation);

            if (entry == null)
            {
                ThrowInvalidOpRecordingOperationAfterStart();
            }

            Assert(result != null);

            return result;
        }

        private async Task<bool> TryApplyOperationAsync(IOperation operation, CancellationToken cancellation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            bool Condition(ITransactionState current)
            {
                // We must include the committed state here because otherwise sucess will be false and the caller asumes the transaction was aborted.
                // This is not problematic as a concurrently committed transaction leading our action to be a nop.
                // We do not include the CleanedUp state with reasoning. See: TransactionalStore.ApplyOperationsAsync()
                return current.Status == TransactionStatus.Pending || current.Status == TransactionStatus.Committed;
            }

            (ITransactionState entry, bool success) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Apply(current, operation), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            if (entry == null) // TODO: Can we return false instead?
            {
                ThrowTransactionNotExistent();
            }

            return success;
        }

        private async Task<bool> TryUnapplyOperationAsync(IOperation operation, CancellationToken cancellation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            bool Condition(ITransactionState current)
            {
                return current.Status == TransactionStatus.AbortRequested;
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Unapply(current, operation), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return success; // TODO: What to do if entry is null?
        }

        private static void ThrowInvalidOpRecordingOperationAfterStart()
        {
            throw new InvalidOperationException("Cannot record operations after a transaction has started.");
        }

        private static void ThrowTransactionNotExistent()
        {
            throw new InvalidOperationException("Cannot operate on a non-existing transaction.");
        }

        #endregion

        #region UpdateEntry

        public async Task<ITransactionState> UpdateAsync(CancellationToken cancellation)
        {
            var entry = await _transactionStorage.GetTransactionAsync(Id, cancellation);

            return Update(entry);
        }

        internal ITransactionState Update(ITransactionState entry)
        {
            // TODO
            if (entry == null)
            {
                // TODO: Remove transaction from store.
                // TODO: Can we set _entry to null?

                return null;
            }

            lock (_entryLock)
            {
                // We cannot assume that we read an entry with a larger or the same version, as there may be concurrent reads.
                if (_entryVersion < entry.Version)
                {
                    _entry = entry;
                    _entryVersion = entry.Version;

                    return entry;
                }

                return _entry;
            }
        }

        private Task<ITransactionState> UpdateEntry(Func<ITransactionState, ITransactionState> update,
                                                    CancellationToken cancellation)
        {
            return UpdateEntry(update, condition: _ => true, cancellation);
        }

        private async Task<ITransactionState> UpdateEntry(Func<ITransactionState, ITransactionState> update,
                                                          Func<ITransactionState, bool> condition,
                                                          CancellationToken cancellation)
        {
            var (result, _) = await UpdateEntry<object>(state => (update(state), null), condition, cancellation);
            return result;
        }

        #endregion

        #region Equality

        public override bool Equals(object obj)
        {
            return obj is Transaction transaction && Equals(transaction);
        }

        public bool Equals(Transaction other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(other, this))
                return true;

            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(Transaction left, Transaction right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(Transaction left, Transaction right)
        {
            if (left is null)
                return !(right is null);

            return !left.Equals(right);
        }

        #endregion

        #region Commit process

        private enum ProcessingState { Committed, Aborted };

        private async ValueTask<ProcessingState> ProcessAsync(CancellationToken cancellation)
        {
            var processedTransactions = new HashSet<Transaction>();
            var visitedTransactions = new HashSet<Transaction>();

            var result = await ProcessAsync(originalTransaction: this,
                                                       processedTransactions,
                                                       visitedTransactions,
                                                       cancellation);

#if DEBUG
            await UpdateAsync(cancellation);
            var state = Status;

            Assert(!visitedTransactions.Any());
            Assert(state == TransactionStatus.Aborted ||
                   state == TransactionStatus.CleanedUp ||
                   state == null); // The garbage collector already collected the transaction
#endif


            return result;
        }

        private async ValueTask<ProcessingState> ProcessAsync(Transaction originalTransaction,
                                                              ISet<Transaction> processedTransactions,
                                                              ISet<Transaction> visitedTransactions,
                                                              CancellationToken cancellation)
        {
            Assert(processedTransactions != null);
            Assert(visitedTransactions != null);

            _logger?.LogTrace($"Processing transaction {Id}");

            await UpdateAsync(cancellation);

            if (Status.IsCommitted())
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {Id} committed successfully.");

                return ProcessingState.Committed;
            }

            if (Status == TransactionStatus.Aborted)
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {Id} aborted.");

                return ProcessingState.Aborted;
            }

            if (Status == TransactionStatus.AbortRequested)
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction abort requested. Aborting transaction {Id}");
                return await AbortInternalAsync(originalTransaction, Operations, cancellation);
            }

            if (visitedTransactions.Contains(this))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Detected cycle. Aborting transaction {Id}");
                return await AbortInternalAsync(originalTransaction, Operations, cancellation);
            }

            var dependencies = new HashSet<Transaction>();
            if (!await ApplyOperationsAsync(Operations, dependencies, cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction abort requested concurrently. Aborting transaction {Id}");
                return await AbortInternalAsync(originalTransaction, Operations, cancellation);
            }

            if (dependencies.Any())
            {
                var stringifiedDependencies = dependencies.Select(p => p.Id.ToString()).Aggregate((e, n) => e + ", " + n);

                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Processing dependencies of transaction {Id}: {stringifiedDependencies}.");
            }

            // Recursively process all dependencies.
            await ProcessDependenciesAsync(originalTransaction, dependencies, processedTransactions, visitedTransactions, cancellation);

#if DEBUG
            Assert(dependencies.All(p => processedTransactions.Contains(p)));

            foreach (var dependency in dependencies)
            {
                await dependency.UpdateAsync(cancellation);

                Assert(dependency.Status.IsCommitted() || dependency.Status == TransactionStatus.Aborted);
            }

#endif

            if (!await CheckVersionsAsync(Operations, cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction cannot be committed due to a version conflict. Aborting transaction {Id}");
                return await AbortInternalAsync(originalTransaction, Operations, cancellation);
            }

            if (!await TryTransitionCommittedStateAsync(cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction abort requested concurrently. Aborting transaction {Id}");
                return await AbortInternalAsync(originalTransaction, Operations, cancellation);
            }

            _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {Id} committed successfully.");

            await CleanUpTransactionAsync(Operations, cancellation);

            return ProcessingState.Committed;
        }

        private async ValueTask<ProcessingState> AbortInternalAsync(Transaction originalTransaction,
                                                                    ImmutableArray<IOperation> operations,
                                                                    CancellationToken cancellation)
        {
            if (!await TryTransitionRequestAbortedStateAsync(cancellation))
            {
                _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {Id} committed successfully.");
                await CleanUpTransactionAsync(operations, cancellation);
                return ProcessingState.Committed;
            }

#if DEBUG
            await UpdateAsync(cancellation);

            Assert(Status == null || Status == TransactionStatus.Aborted || Status == TransactionStatus.AbortRequested);
#endif

            await AbortOperationsAsync(operations, cancellation);
            await TransitionAbortStateAsync(cancellation);

            _logger?.LogTrace($"Processing transaction {originalTransaction.Id}. Transaction {Id} aborted.");

            return ProcessingState.Aborted;
        }

        private async Task CleanUpTransactionAsync(CancellationToken cancellation)
        {
            await UpdateAsync(cancellation);
            await CleanUpTransactionAsync(Operations, cancellation);
        }

        private async Task CleanUpTransactionAsync(ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
            await CommitOperationsAsync(operations, cancellation);
            await TransitionCleanUpStateAsync(cancellation);
        }

        private async Task ProcessDependenciesAsync(Transaction originalTransaction,
                                                    ISet<Transaction> dependencies,
                                                    ISet<Transaction> processedTransactions,
                                                    ISet<Transaction> visitedTransactions,
                                                    CancellationToken cancellation)
        {
            if (!dependencies.Any())
            {
                return;
            }

            visitedTransactions.Add(this);
            foreach (var dependency in dependencies)
            {
                if (processedTransactions.Contains(dependency))
                    continue;

                await dependency.ProcessAsync(originalTransaction, processedTransactions, visitedTransactions, cancellation);

#if DEBUG
                await dependency.UpdateAsync(cancellation);
                Assert(dependency.Status == TransactionStatus.Aborted || dependency.Status.IsCommitted());
#endif

                processedTransactions.Add(dependency);
            }
            var removed = visitedTransactions.Remove(this);

            Assert(removed);
        }

        // True => Commit ; False => Abort
        private async Task<bool> CheckVersionsAsync(ImmutableArray<IOperation> operations, CancellationToken cancellation)
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

                var typedTransaction = GetTypedTransaction(operation.EntryType);
                var originalEntryVersion = await typedTransaction.GetOriginalVersionAsync(operation.Entry, cancellation);

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

            await UpdateAsync(cancellation);


#if DEBUG
            if (notFound)
            {
                Assert(Status != TransactionStatus.Pending);
            }
#endif

            return Status.IsCommitted() || Status == TransactionStatus.Pending;
        }

        // True => Commit ; False => Abort
        private async Task<bool> ApplyOperationsAsync(ImmutableArray<IOperation> operations, ISet<Transaction> dependencies, CancellationToken cancellation)
        {
            Assert(operations != null);
            Assert(dependencies != null);

            foreach (var operation in operations.Shuffle())
            {
                var typedTransaction = GetTypedTransaction(operation.EntryType);
                var isApplied = operation.State == OperationState.Applied;

                // Apply each operation to the respective entry or load the dependencies if the operation is already applied.
                var dependentTransactions = isApplied ?
                                            await typedTransaction.GetDependenciesAsync(operation.Entry, cancellation) :
                                            await typedTransaction.ApplyOperationAsync(operation.Entry, operation.OperationType, cancellation);

                // If we are in the state CleanedUp, dependentTransactions must be null because 
                // 1) GetDependencies cannot find a pending operation for our transaction which is the precondition to get to state CleanedUp
                // 2) ApplyOperation cannot apply a pending operation for our transaction 
                //    because it is not present in the pending transactions collection which is the precondition to get to state CleanedUp

                if (dependentTransactions == null)
                {
                    await UpdateAsync(cancellation);

                    // If we got here, we cannot be in state pending any more because
                    // 1) GetDependencies does not find a pending operation for our transaction. 
                    //    This can only occur if the pending operation was removed. 
                    //    To be able to remove a pending operation, the respective transaction must either be in state AbortRequested or Committed.
                    // 2) ApplyOperation cannot apply a pending operation for our transaction 
                    //    because it is not present in the pending transactions collection.
                    //    To remove the transaction from the pending transactions collection, the respective pending operation must be removed from the entry
                    //    To be able to remove a pending operation, the respective transaction must either be in state AbortRequested or Committed.

                    Assert(Status != TransactionStatus.Pending);

                    return Status.IsCommitted();
                }

                if (dependentTransactions.Any())
                {
                    dependencies.UnionWith(dependentTransactions);
                }

                if (!isApplied && !await TryApplyOperationAsync(operation, cancellation))
                {
                    await typedTransaction.AbortOperationAsync(operation.Entry, cancellation);

                    return false;
                }
            }

            return true;
        }

        private async Task AbortOperationsAsync(ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
            Assert(operations != null);

            foreach (var operation in operations.Shuffle())
            {
                // Cannot be done as it must be ensured that transaction is removes from the pending transactions collection.
                //if (operation.State == OperationState.Unapplied)
                //    continue;

                // Unapply each operation from the respective entry
                var typedTransaction = GetTypedTransaction(operation.EntryType);

                await typedTransaction.AbortOperationAsync(operation.Entry, cancellation);
                if (!await TryUnapplyOperationAsync(operation, cancellation))
                {
                    // The transaction aborted concurrently.

                    break;
                }
            }
        }

        private async Task CommitOperationsAsync(ImmutableArray<IOperation> operations, CancellationToken cancellation)
        {
            Assert(operations != null);

            foreach (var operation in operations.Shuffle())
            {
                // Commit each operation to the respective entry
                var typedTransaction = GetTypedTransaction(operation.EntryType);

                await typedTransaction.CommitOperationAsync(operation.Entry, cancellation);
            }
        }

        #endregion

        #region ITransaction

        public IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
           where TData : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            if (!OwnsTransaction)
                throw new InvalidOperationException("Unable to perform operation on foreign transaction.");

            return GetTypedTransaction<TData>().GetAsync(predicate, cancellation);
        }

        public Task StoreAsync<TData>(TData data, CancellationToken cancellation)
            where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!OwnsTransaction)
                throw new InvalidOperationException("Unable to perform operation on foreign transaction.");

            return GetTypedTransaction<TData>().StoreAsync(data, cancellation);
        }

        public Task RemoveAsync<TData>(TData data, CancellationToken cancellation)
                where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (!OwnsTransaction)
                throw new InvalidOperationException("Unable to perform operation on foreign transaction.");

            return GetTypedTransaction<TData>().RemoveAsync(data, cancellation);
        }

        public async Task<bool> TryCommitAsync(CancellationToken cancellation)
        {
            if (!OwnsTransaction)
                throw new InvalidOperationException("Unable to perform operation on foreign transaction.");

            await PrepareAsync(cancellation);

            return await ProcessAsync(cancellation) == ProcessingState.Committed;
        }

        public async Task AbortAsync(CancellationToken cancellation)
        {
            if (!OwnsTransaction)
                throw new InvalidOperationException("Unable to perform operation on foreign transaction.");

            if (await TryTransitionRequestAbortedStateAsync(cancellation))
            {
                await ProcessAsync(cancellation);
            }
        }

        private async Task PrepareAsync(CancellationToken cancellation)
        {
            // The commit operation must not be called concurrently. 
            // Otherwise it is possible that transactions are added as pending transactions to entry after the transaction committed 
            // or abort is requested and its pending operations are removed leading to various unconcistencies.
            using (await _lock.LockAsync(cancellation))
            {
                if (await TryTransitionPrepareStateAsync(cancellation))
                {
                    // As this is our own transaction, we do not need to update.
                    // All operations, the transaction may contains are issued by us.
                    var operations = Operations;

                    foreach (var operation in operations.Shuffle())
                    {
                        // Add transaction to pending transactions
                        var typedStore = GetTypedTransaction(operation.EntryType);

                        await typedStore.AddPendingTransactionAsync(operation.Entry, cancellation);
                    }
                }

                await TryTransitionPendingStateAsync(cancellation);
            }
        }

        #endregion

        #region TypedTransaction

        private ITypedTransaction GetTypedTransaction(Type dataType)
        {
            return _typedTransactions.GetOrAdd(dataType, CreateTypedTransaction);
        }

        private ITypedTransaction<TData> GetTypedTransaction<TData>()
                 where TData : class
        {
            return (ITypedTransaction<TData>)_typedTransactions.GetOrAdd(typeof(TData), CreateTypedTransaction);
        }

        private ITypedTransaction CreateTypedTransaction(Type dataType)
        {
            Assert(dataType != null);

            var idType = DataPropertyHelper.GetIdType(dataType);

            var typedStore = Activator.CreateInstance(typeof(TypedTransaction<,>).MakeGenericType(idType, dataType), this);

            return (ITypedTransaction)typedStore;
        }

        private interface ITypedTransaction
        {
            Task AddPendingTransactionAsync(object data, CancellationToken cancellation);
            Task<IEnumerable<Transaction>> ApplyOperationAsync(object data, OperationType operationType, CancellationToken cancellation);
            Task<IEnumerable<Transaction>> GetDependenciesAsync(object data, CancellationToken cancellation);
            Task AbortOperationAsync(object data, CancellationToken cancellation);
            Task CommitOperationAsync(object data, CancellationToken cancellation);
            Task<int?> GetOriginalVersionAsync(object data, CancellationToken cancellation);
        }

        private interface ITypedTransaction<TData> : ITypedTransaction
                     where TData : class
        {
            Task StoreAsync(TData data, CancellationToken cancellation);
            Task RemoveAsync(TData data, CancellationToken cancellation);
            IAsyncEnumerable<TData> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        }

        private sealed partial class TypedTransaction<TId, TData> : ITypedTransaction<TData>
             where TData : class
        {
            #region Fields

            private readonly Transaction _transaction;
            private readonly TransactionManager _transactionManager;
            private readonly ITransactionStateStorage _transactionStorage;
            private readonly IEntryStateStorage<TId, TData> _entryStorage;
            private readonly IEntryStateTransformer<TId, TData> _entryStateTransformer;
            private readonly ILogger<TypedTransaction<TId, TData>> _logger;

            // This is null for transactions that we do not own to prevent bugs.
            private readonly Dictionary<TId, IEntrySnapshot<TId, TData>> _snapshots;
            private readonly ISet<Transaction> _nonCommittedTransactions;

            #endregion

            #region C'tor

            public TypedTransaction(Transaction transaction)
            {
                Assert(transaction != null);

                _transaction = transaction;
                _transactionManager = transaction._transactionManager;
                _transactionStorage = transaction._transactionStorage;
                _entryStateTransformer = transaction._entryStateTransformerFactory.GetEntryManager<TId, TData>();
                _entryStorage = transaction._entryStateStorageFactory.GetEntryStorage<TId, TData>();
                var loggerFactory = transaction._loggerFactory;

                _logger = loggerFactory?.CreateLogger<TypedTransaction<TId, TData>>();

                Assert(_transactionStorage != null);
                Assert(_entryStateTransformer != null);
                Assert(_entryStorage != null);

                if (_transaction.OwnsTransaction)
                {
                    var idEqualityComparer = new IdEqualityComparer<TId>();
                    _snapshots = new Dictionary<TId, IEntrySnapshot<TId, TData>>(idEqualityComparer);
                    _nonCommittedTransactions = _transaction._nonCommittedTransactions;
                }
            }

            #endregion

            #region ITypedTransaction

            public Task AddPendingTransactionAsync(object data, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type '{typeof(TData).FullName}' or a derived type.");
                }

                return AddPendingTransactionAsync(typedData, cancellation);
            }

            public Task<IEnumerable<Transaction>> GetDependenciesAsync(object data, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return GetDependenciesAsync(typedData, cancellation);
            }

            public Task<int?> GetOriginalVersionAsync(object data, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return GetOriginalVersionAsync(typedData, cancellation);
            }

            public Task<IEnumerable<Transaction>> ApplyOperationAsync(object data, OperationType operationType, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return ApplyOperationAsync(typedData, operationType, cancellation);
            }

            public Task AbortOperationAsync(object data, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return AbortOperationAsync(typedData, cancellation);
            }

            public Task CommitOperationAsync(object data, CancellationToken cancellation)
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!(data is TData typedData))
                {
                    throw new ArgumentException($"The specified object must be of type {typeof(TData).FullName} or a derived type.");
                }

                return CommitOperationAsync(typedData, cancellation);
            }

            #endregion

            #region ITypedTransaction<TData>

            public async Task StoreAsync(TData data, CancellationToken cancellation)
            {
                Assert(data != null);

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entryPresent = _snapshots.TryGetValue(id, out var entry);
                var expectedVersion = GetExpectedVersion(entryPresent ? entry : null);
                UpdateIdMap(data, id, entryPresent, entry);

                try
                {
                    await _transaction.StoreOperationAsync(data, expectedVersion, cancellation);
                }
                catch
                {
                    if (entryPresent)
                    {
                        Assert(entry.Data != null);
                        _snapshots[id] = entry;
                    }
                    else
                    {
                        _snapshots.Remove(id);
                    }

                    throw;
                }
            }

            public async Task RemoveAsync(TData data, CancellationToken cancellation)
            {
                Assert(data != null);

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entryPresent = _snapshots.TryGetValue(id, out var entry);
                var expectedVersion = GetExpectedVersion(entryPresent ? entry : null);
                UpdateIdMap(data, id, entryPresent, entry);

                try
                {
                    await _transaction.DeleteOperationAsync(data, expectedVersion, cancellation);
                }
                catch
                {
                    if (entryPresent)
                    {
                        Assert(entry.Data != null);
                        _snapshots[id] = entry;
                    }
                    else
                    {
                        _snapshots.Remove(id);
                    }

                    throw;
                }
            }

            public IAsyncEnumerable<TData> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
            {
                return new AsyncEnumerable<TData>(() => GetAsyncInternal(predicate, cancellation));
            }

            #endregion

            private Task AddPendingTransactionAsync(TData data, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var predicate = DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id);

                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.AddPendingTransaction(c, _transaction.Id);
                }

                bool Condition(IEntryState<TId, TData> c)
                {
                    return !c.PendingTransactions.Contains(_transaction.Id);
                }

                return _entryStorage.UpdateEntryAsync(predicate, Update, Condition, cancellation).AsTask();
            }

            private async Task<IEnumerable<Transaction>> GetDependenciesAsync(TData data, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entry = await _entryStorage.GetEntryAsync(DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id), cancellation);
                ISet<long> dependencies = new HashSet<long>();

                if (entry == null)
                {
                    return Enumerable.Empty<Transaction>();
                }

                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    var pendingOperation = entry.PendingOperations[i];

                    if (pendingOperation.TransactionId == _transaction.Id)
                    {
                        return await Task.WhenAll(dependencies.Select(p => _transactionManager.GetTransactionAsync(p, cancellation).AsTask()));
                    }

                    dependencies.Add(pendingOperation.TransactionId);
                }

                return null;
            }

            private async Task<int?> GetOriginalVersionAsync(TData data, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entry = await _entryStorage.GetEntryAsync(DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id), cancellation);
                Assert(entry != null);

                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    var pendingOperation = entry.PendingOperations[i];

                    if (pendingOperation.TransactionId == _transaction.Id)
                    {
                        return pendingOperation.OriginalData.DataVersion;
                    }

#if DEBUG
                    var tx = await _transactionManager.GetTransactionAsync(pendingOperation.TransactionId, cancellation);
                    await tx.UpdateAsync(cancellation);
                    var txState = tx.Status;

                    if (txState != TransactionStatus.Committed)
                    {
                        await _transaction.UpdateAsync(cancellation);
                        var transactionState = _transaction.Status;

                        Assert(transactionState == TransactionStatus.Aborted ||
                               transactionState == TransactionStatus.AbortRequested ||
                               transactionState.IsCommitted());
                    }
#endif
                }

                return null;
            }

            private async Task<IEnumerable<Transaction>> ApplyOperationAsync(TData data, OperationType operationType, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                IEntryState<TId, TData> entry, desired = null;
                ISet<long> dependencies;

                do
                {
                    dependencies = new HashSet<long>();
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
                            desired = _entryStateTransformer.Create(id, data, _transaction.Id);
                        }
                    }
                    else
                    {
                        if (!entry.PendingTransactions.Contains(_transaction.Id))
                        {
                            return null;
                        }

                        for (var i = 0; i < entry.PendingOperations.Count; i++)
                        {
                            var pendingOperation = entry.PendingOperations[i];

                            if (pendingOperation.TransactionId == _transaction.Id)
                            {
                                goto BUILD_RESULT;
                            }

                            dependencies.Add(pendingOperation.TransactionId);
                        }

                        var deletion = operationType == OperationType.Delete;

                        desired = _entryStateTransformer.Store(entry, deletion ? null : data, _transaction.Id);
                    }

                    Assert(desired != null);
                }
                while (!await _entryStorage.CompareExchangeAsync(desired, entry, cancellation));

                _logger?.LogTrace($"Applied operation for transaction {_transaction.Id} to entry {id}.");

                Assert(desired.PendingOperations.Last().TransactionId == _transaction.Id);

BUILD_RESULT:

                return await Task.WhenAll(dependencies.Select(p => _transactionManager.GetTransactionAsync(p, cancellation).AsTask()));
            }

            private async Task AbortOperationAsync(TData data, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var predicate = DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id);

                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.Abort(c, _transaction.Id);
                }

                var entry = await _entryStorage.UpdateEntryAsync(predicate, Update, cancellation);

                _logger?.LogTrace($"Aborted operation for transaction {_transaction.Id} to entry {id}.");

                Assert(!entry.PendingOperations.Any(p => p.TransactionId == _transaction.Id));
            }

            private async Task CommitOperationAsync(TData data, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var predicate = DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id);

                IEntryState<TId, TData> Update(IEntryState<TId, TData> current)
                {
                    return _entryStateTransformer.Commit(current, _transaction.Id);
                }

                await _entryStorage.UpdateEntryAsync(predicate, Update, cancellation);

                _logger?.LogTrace($"Committed operation for transaction {_transaction.Id} to entry {id}.");
            }

            private int? GetExpectedVersion(IEntrySnapshot<TId, TData> idMapEntry)
            {
                if (idMapEntry == null)
                {
                    return default;

                    //       If the store operation is performed, and the entry was not loaded before, it is not present in the id map.
                    //       In this case we currently return no expectedVersion.
                    //       Do problems arise if we load the object after the store operation?
                    //       There are several options if this is a problem:
                    //       1) Load operations do respect store operations done previously in the current transaction even if the transaction is not yet committed.
                    //       2) If the entry is not present in the id map, we load it an use its version as expected version.
                    //       3) If we store the object, we remember that we stored an operation with no expected version. When we perform a load operation afterwards, 
                    //          we modify the already stored operation.

                    // Currently Option (1) is implemented.
                }

                return idMapEntry.DataVersion;
            }

            private void UpdateIdMap(TData data, TId id, bool entryPresent, IEntrySnapshot<TId, TData> entry)
            {
                _snapshots[id] = GetIdMapEntry(data, id, entryPresent, entry);
            }

            private static IEntrySnapshot<TId, TData> GetIdMapEntry(TData data, TId id, bool entryPresent, IEntrySnapshot<TId, TData> entry)
            {
                IEntrySnapshot<TId, TData> result;

                if (!entryPresent)
                {
                    return new UpdatedSnapshot(id, data);
                }
                else if (entry is UpdatedSnapshot updatedSnapshot)
                {
                    result = new UpdatedSnapshot(updatedSnapshot.OriginalSnapshot, data);
                }
                else
                {
                    result = new UpdatedSnapshot(entry, data);
                }

                Assert(result.Data != null);

                return result;
            }

            private async AsyncEnumerator<TData> GetAsyncInternal(Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
            {
                Assert(predicate != null);

                var yield = await AsyncEnumerator<TData>.Capture();

                var lamda = BuildPredicate(predicate);
                var queryExpression = BuildQueryExpression(predicate);
                var compiledLambda = lamda.Compile();
                var idEqualityComparer = new IdEqualityComparer<TId>();
                var result = new Dictionary<TId, IEntrySnapshot<TId, TData>>(idEqualityComparer);

                // This is not parallelized with Task.WhenAll with care.
                var entries = _entryStorage.GetEntriesAsync(queryExpression, cancellation);
                IAsyncEnumerator<IEntryState<TId, TData>> entriesEnumerator = null;

                try
                {
                    entriesEnumerator = entries.GetEnumerator();

                    while (await entriesEnumerator.MoveNext(cancellation))
                    {
                        var entry = entriesEnumerator.Current;
                        var snapshot = await ProcessEntryAsync(entry, compiledLambda, cancellation);

                        if (snapshot != null)
                        {
                            if (result.TryAdd(snapshot.Id, snapshot))
                            {
                                await yield.Return(snapshot.Data);
                            }
                        }
                    }
                }
                finally
                {
                    entriesEnumerator?.Dispose();
                }

                // Prevent phantom reads when other transactions deleted an entry.
                foreach (var entry in _snapshots.Where(p => compiledLambda(p.Value)))
                {
                    if (result.TryAdd(entry.Key, entry.Value))
                    {
                        await yield.Return(entry.Value.Data);
                    }
                }

                // ProcessNonCommittedTransactionsAsync
                var transactions = LoadNonCommittedTransactionsAsync(cancellation);
                var transactionsEnumerator = transactions.GetEnumerator();

                try
                {
                    while (await transactionsEnumerator.MoveNext(cancellation))
                    {
                        var transaction = transactionsEnumerator.Current;
                        await transaction.UpdateAsync(cancellation);
                        var operations = transaction.Operations.Where(p => p.EntryType == typeof(TData));

                        foreach (var operation in operations)
                        {
                            Assert(operation.Entry is TData);

                            var id = DataPropertyHelper.GetId<TId, TData>(operation.Entry as TData);

                            if (result.ContainsKey(id))
                            {
                                continue;
                            }

                            // It is not neccessary to check entries of the id map because ProcessEntriesAsync already processed the complete id map.
                            if (!_snapshots.TryGetValue(id, out _))
                            {
                                var entry = await _entryStorage.GetEntryAsync(id, cancellation);

                                //Assert(entry != null);

                                if (entry != null && // TODO: Is this the correct behavior?
                                    entry.CreatingTransaction < _transaction.Id)
                                {
                                    // TODO: Do we need to reload added transactions?
                                    var snapshot = await GetCommittedSnapshotAsync(entry, compiledLambda, cancellation);

                                    if (snapshot != null)
                                    {
                                        if (result.TryAdd(id, snapshot))
                                        {
                                            await yield.Return(snapshot.Data);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    transactionsEnumerator.Dispose();
                }

                return yield.Break();
            }

            private IAsyncEnumerable<Transaction> LoadNonCommittedTransactionsAsync(CancellationToken cancellation)
            {
                return new AsyncEnumerable<Transaction>(() => LoadNonCommittedTransactionsCoreAsync(cancellation));
            }

            private async AsyncEnumerator<Transaction> LoadNonCommittedTransactionsCoreAsync(CancellationToken cancellation)
            {
                var yield = await AsyncEnumerator<Transaction>.Capture();

                foreach (var transaction in _nonCommittedTransactions)
                {
                    await yield.Return(transaction);
                }

                var transactions = _transactionManager.GetNonCommittedTransactionsAsync(cancellation);

                await transactions.ForeachAsync(async transaction =>
                {
                    if (_nonCommittedTransactions.Add(transaction))
                    {
                        await yield.Return(transaction);
                    }
                });

                return yield.Break();
            }


            private Expression<Func<IEntryState<TId, TData>, bool>> BuildQueryExpression(Expression<Func<TData, bool>> predicate)
            {
                // The resulting predicate checks
                // 1) If the id of the transaction that created the entry is smaller than the current transactions id
                //    to prevent phantom reads when other transactions add an entry.
                // 2) If the entries payload matches the specified predicate.
                // 3) If the entries data is not null (The entry is not deleted)
                var parameter = Expression.Parameter(typeof(IEntryState<TId, TData>));

                var creationTransactionAccessor = GetCreationTransactionAccessor();
                var creationTransaction = ParameterExpressionReplacer.ReplaceParameter(creationTransactionAccessor.Body, creationTransactionAccessor.Parameters.First(), parameter);
                var transactionComparison = Expression.LessThan(creationTransaction, Expression.Constant(_transaction.Id));

                var dataAccessor = GetDataAccessor();
                var data = ParameterExpressionReplacer.ReplaceParameter(dataAccessor.Body, dataAccessor.Parameters.First(), parameter);
                var isDeleted = Expression.ReferenceEqual(data, Expression.Constant(null, typeof(TData)));
                var body = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), data);
                return Expression.Lambda<Func<IEntryState<TId, TData>, bool>>(Expression.AndAlso(Expression.Not(isDeleted), Expression.AndAlso(transactionComparison, body)), parameter);
            }

            private static Expression<Func<IEntryState<TId, TData>, long>> GetCreationTransactionAccessor()
            {
                return p => p.CreatingTransaction;
            }

            private static Expression<Func<IEntryState<TId, TData>, TData>> GetDataAccessor()
            {
                return p => p.Data;
            }

            // We have to process the entries for the following reason.
            // One / several transactions modified the entry but the transactions are not fully committed yet and the entry MATCHED the predicate.
            // We have to check whether the entry also matched without the changes that the transactions produced to prevent a dirty read.
            private async ValueTask<IEntrySnapshot<TId, TData>> ProcessEntryAsync(IEntryState<TId, TData> entry,
                                                                                  Func<IDataRepresentation<TId, TData>, bool> predicate,
                                                                                  CancellationToken cancellation)
            {
                Assert(entry != null);

                IEntrySnapshot<TId, TData> result;

                // Prevent phantom reads when other transactions modify an entry.
                if (_snapshots.ContainsKey(entry.Id))
                {
                    // ProcessEntriesAsync will check all cached entries to match the predicate => We can skip this here.
                    result = null;
                }
                else
                {
                    result = await GetCommittedSnapshotAsync(entry, predicate, cancellation);
                }

                if (result != null)
                {
                    Assert(result.Data != null);

                    _snapshots.Add(result.Id, result);
                }

                return result;
            }

            // One / several transactions may have modified the entry but the transactions are not fully committed yet and the entry MATCHED the predicate.
            // We have to check whether the entry also matched without the changes that the transactions produced to prevent a dirty read.
            private async ValueTask<IEntrySnapshot<TId, TData>> GetCommittedSnapshotAsync(IEntryState<TId, TData> entry,
                                                                                          Func<IDataRepresentation<TId, TData>, bool> predicate,
                                                                                          CancellationToken cancellation)
            {
                var committedTransactions = new List<long>();
                var result = _entryStateTransformer.ToSnapshot(entry);

                Assert(result != null);

                // PendingOperations are ordered by OriginalDataVersion
                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    var pendingOperation = entry.PendingOperations[i];
                    var transaction = await _transactionManager.GetTransactionAsync(pendingOperation.TransactionId, cancellation);
                    var originalData = pendingOperation.OriginalData;

                    if (_nonCommittedTransactions.Contains(transaction))
                    {
                        result = originalData.Data != null && predicate(originalData) ? originalData : null;

                        break;
                    }

                    await transaction.UpdateAsync(cancellation);
                    var transactionState = transaction.Status;

                    // If the transaction is not present or its state is committed, the transaction is already committed, it is just not removed from the collection.
                    if (!transactionState.IsCommitted())
                    {
                        // An initial transaction must not have pending operations.
                        Assert(transactionState != TransactionStatus.Initial);

                        // The pending operation does no create the entry and the previous data matches the predicate.
                        result = originalData.Data != null && predicate(originalData) ? originalData : null;

                        if (transactionState == TransactionStatus.AbortRequested || transactionState == TransactionStatus.Aborted)
                        {
#if DEBUG
                            // After an operation thats transaction is aborted MUST NOT follow an operation that belongs to a committed transaction.
                            // The transaction commit operation must ensure this. 
                            // Because of this, it is the same case than it were if the operation belongs to a pending transaction.

                            if (i < entry.PendingOperations.Count - 1)
                            {
                                var nextOperation = entry.PendingOperations[i + 1];
                                var nextOperationTransaction = await _transactionManager.GetTransactionAsync(nextOperation.TransactionId, cancellation);

                                await nextOperationTransaction.UpdateAsync(cancellation);
                                var nextOperationTransactionState = nextOperationTransaction.Status;

                                // TODO: Assert failed nextOperationTransactionState == TransactionStatus.CleanedUp
                                Assert(nextOperationTransactionState != null &&
                                       nextOperationTransactionState != TransactionStatus.CleanedUp &&
                                       nextOperationTransactionState != TransactionStatus.Initial &&
                                       nextOperationTransactionState != TransactionStatus.Committed);
                            }
#endif

                            await AbortAsync(entry, transaction, cancellation);
                        }

                        _nonCommittedTransactions.Add(transaction);

                        break;
                    }

                    committedTransactions.Add(pendingOperation.TransactionId);
                }

                var byIdSelector = DataPropertyHelper.CompilePredicate(typeof(TData), entry.Data);

                foreach (var transaction in _nonCommittedTransactions)
                {
                    var operations = transaction.Operations;

                    // TODO: Why does the == on the type objects does not work here?
                    /*p.EntryType == typeof(TData)*/
                    if (!operations.Any(p => AreEqual(p.EntryType, typeof(TData)) && byIdSelector(p))) // TODO: ArgumentNullException for p
                        continue;

                    // The transaction is either non-committed or viewed as non-committed by the current transaction.
                    // If the operation aborts or completed in the meantime, all pending operation belongin to the repspective
                    // transaction may be removed. We do not known whether the transaction is aborted or committed in the described case and
                    // We cannot obtain the original data of the entry.
                    // We must abort the current transaction as we cannot ensure consistency.
                    // We cannot prevent this situation without disturbing the more major case that this relatively rare case
                    // does not occur but we can investigate with saving the original data when we save the transaction in the
                    // non-committed collection.
                    if (!entry.PendingOperations.Any(p => p.TransactionId == _transaction.Id))
                    {
                        await _transaction.AbortAsync(cancellation);
                        throw new TransactionAbortedException();
                    }
                }

                if (committedTransactions.Any())
                {
                    await CommitAllAsync(entry, committedTransactions, cancellation);
                }

                return result;
            }

            private static bool AreEqual(Type left, Type right)
            {
                return left.IsAssignableFrom(right) && right.IsAssignableFrom(left);
            }

            private ValueTask<IEntryState<TId, TData>> CommitAllAsync(IEntryState<TId, TData> entry,
                                                            IEnumerable<long> transactionIds,
                                                            CancellationToken cancellation)
            {
                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.CommitAll(c, transactionIds);
                }

                return _entryStorage.UpdateEntryAsync(entry, Update, cancellation);
            }

            private async ValueTask<IEntryState<TId, TData>> AbortAsync(IEntryState<TId, TData> entry,
                                                              Transaction transaction,
                                                              CancellationToken cancellation)
            {
                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.Abort(c, transaction.Id);
                }

                entry = await _entryStorage.UpdateEntryAsync(entry, Update, cancellation);

                Assert(!entry.PendingOperations.Any(p => p.TransactionId == transaction.Id));

                return entry;
            }

            private static Expression<Func<IDataRepresentation<TId, TData>, bool>> BuildPredicate(Expression<Func<TData, bool>> predicate)
            {
                Expression<Func<IDataRepresentation<TId, TData>, TData>> dataSelector = entry => entry.Data;
                var body = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), dataSelector.Body);
                var parameter = dataSelector.Parameters.First();
                return Expression.Lambda<Func<IDataRepresentation<TId, TData>, bool>>(body, parameter);
            }

            private sealed class UpdatedSnapshot : IEntrySnapshot<TId, TData>
            {
                public UpdatedSnapshot(IEntrySnapshot<TId, TData> original, TData data)
                {
                    OriginalSnapshot = original;
                    Id = OriginalSnapshot.Id;
                    DataVersion = original.DataVersion;
                    LastWriteTime = original.LastWriteTime;
                    Data = data;
                }

                public UpdatedSnapshot(TId id, TData data)
                {
                    OriginalSnapshot = null;
                    Id = id;
                    DataVersion = default;
                    LastWriteTime = default;
                    Data = data;
                }

                public IEntrySnapshot<TId, TData> OriginalSnapshot { get; }

                public int DataVersion { get; }

                public DateTime? LastWriteTime { get; }

                public TId Id { get; }

                public TData Data { get; }
            }
        }

        #endregion
    }

    public sealed class TransactionManager : ITransactionManager
    {
        private readonly ITransactionStateStorage _transactionStorage;
        private readonly ITransactionStateTransformer _transactionStateTransformer;
        private readonly IEntryStateStorageFactory _entryStateStorageFactory;
        private readonly IEntryStateTransformerFactory _entryStateTransformerFactory;
        private readonly ILoggerFactory _loggerFactory;

        private readonly WeakDictionary<long, Transaction> _transactions = new WeakDictionary<long, Transaction>();

        public TransactionManager(ITransactionStateStorage transactionStorage,
                                   ITransactionStateTransformer transactionStateTransformer,
                                   IEntryStateStorageFactory entryStateStorageFactory,
                                   IEntryStateTransformerFactory entryStateTransformerFactory,
                                   ILoggerFactory loggerFactory = null)
        {
            if (transactionStorage == null)
                throw new ArgumentNullException(nameof(transactionStorage));

            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            if (entryStateStorageFactory == null)
                throw new ArgumentNullException(nameof(entryStateStorageFactory));

            if (entryStateTransformerFactory == null)
                throw new ArgumentNullException(nameof(entryStateTransformerFactory));

            _transactionStorage = transactionStorage;
            _transactionStateTransformer = transactionStateTransformer;
            _entryStateStorageFactory = entryStateStorageFactory;
            _entryStateTransformerFactory = entryStateTransformerFactory;
            _loggerFactory = loggerFactory;
        }

        internal Transaction BuildTransaction(ITransactionState entry)
        {
            return new Transaction(entry.Id,
                                    ownsTransaction: false,
                                    entry,
                                    this,
                                    _transactionStorage,
                                    _transactionStateTransformer,
                                    _entryStateStorageFactory,
                                    _entryStateTransformerFactory,
                                    _loggerFactory);
        }

        internal Transaction GetTransaction(ITransactionState entry)
        {
            var transaction = _transactions.GetOrAdd(entry.Id, _ => BuildTransaction(entry));
            transaction.Update(entry);
            return transaction;
        }

        // Load the transaction with the specified id from the underlying database.
        public async ValueTask<Transaction> GetTransactionAsync(long id, CancellationToken cancellation)
        {
            if (_transactions.TryGetValue(id, out var transaction))
            {
                return transaction;
            }

            var entry = await _transactionStorage.GetTransactionAsync(id, cancellation);

            return GetTransaction(entry);
        }

        public IAsyncEnumerable<Transaction> GetNonCommittedTransactionsAsync(CancellationToken cancellation)
        {
            var transactions = _transactionStorage.GetNonCommittedTransactionsAsync(cancellation);

            return transactions.Select(p => GetTransaction(p));
        }

        // New up a fresh transaction that we own and may perform operations on.
        public async ValueTask<Transaction> CreateTransactionAsync(CancellationToken cancellation)
        {
            var id = await _transactionStorage.GetUniqueTransactionIdAsync(cancellation);
            var entry = _transactionStateTransformer.Create(id);
            var result = await _transactionStorage.CompareExchangeAsync(entry, null, cancellation);

            Assert(result);

            return new Transaction(id,
                                    ownsTransaction: true,
                                    entry,
                                    this,
                                    _transactionStorage,
                                    _transactionStateTransformer,
                                    _entryStateStorageFactory,
                                    _entryStateTransformerFactory,
                                    _loggerFactory);
        }

        async ValueTask<ITransaction> ITransactionManager.CreateTransactionAsync(CancellationToken cancellation)
        {
            // TODO: Can we cast this without async/await?
            return await CreateTransactionAsync(cancellation);
        }

        IAsyncEnumerable<ITransaction> ITransactionManager.GetNonCommittedTransactionsAsync(CancellationToken cancellation)
        {
            return GetNonCommittedTransactionsAsync(cancellation);
        }

        async ValueTask<ITransaction> ITransactionManager.GetTransactionAsync(long id, CancellationToken cancellation)
        {
            // TODO: Can we cast this without async/await?
            return await GetTransactionAsync(id, cancellation);
        }
    }
}
