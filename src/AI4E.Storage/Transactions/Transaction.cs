using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using AI4E.Utils.AsyncEnumerable;
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

        #region The following is null for transactions that we do not own to prevent bugs.

        private readonly ISet<Transaction> _nonCommittedTransactions;
        private readonly AsyncLock _lock;

        #endregion
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

            if (_logger.IsEnabled(LogLevel.Trace) && dependencies.Any())
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

#if DEBUG
                await UpdateAsync(cancellation);
                Assert(Status.IsCommitted());
#endif

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

            #region Transaction processing

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
                ISet<long> dependencies = new HashSet<long>();

                do
                {
                    dependencies.Clear();
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

            #endregion

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
                var query = DataRepresentationHelper.TranslatePredicate<TId, TData, IDataRepresentation<TId, TData>>(predicate);
                var translatedPredicate = query.Compile();

                // Prevent phantom reads when other transactions deleted an entry.
                foreach (var entry in _snapshots.Where(p => translatedPredicate(p.Value)))
                {
                    await yield.Return(entry.Value.Data);
                }

                // This is not parallelized with Task.WhenAll on purpose.
                var entries = _entryStorage.GetEntriesAsync(TranslateQuery(query), cancellation);
                IAsyncEnumerator<IEntryState<TId, TData>> entriesEnumerator = null;

                try
                {
                    entriesEnumerator = entries.GetEnumerator();

                    while (await entriesEnumerator.MoveNext(cancellation))
                    {
                        var entry = entriesEnumerator.Current;
                        Assert(entry.Data != null);

                        // We are in the recording state and therefore cannot have created an entry already.
                        Assert(entry.CreatingTransaction != _transaction.Id);

                        // Prevent phantom reads. We ignore all entries that were created of a transaction with a greater transaction id than our transaction's id.
                        if (entry.CreatingTransaction > _transaction.Id)
                        {
                            var creatingTransaction = await _transactionManager.GetTransactionAsync(entry.CreatingTransaction, cancellation);
                            _nonCommittedTransactions.Add(creatingTransaction);
                            continue;
                        }

                        // It is not neccessary to check entries of the id map because we already processed the complete id map.
                        if (_snapshots.ContainsKey(entry.Id))
                        {
                            continue;
                        }

                        var snapshot = await GetCommittedSnapshotAsync(entry, translatedPredicate, checkEntry: false, cancellation);

                        if (snapshot == null)
                        {
                            continue;
                        }

                        Assert(snapshot.Data != null);
                        _snapshots.Add(snapshot.Id, snapshot);
                        await yield.Return(snapshot.Data);
                    }
                }
                finally
                {
                    entriesEnumerator?.Dispose();
                }

                // It is possible that there are entries that actually match our query but are not
                // contained in the set of entries we get from the database as result.
                // This is possible if another transaction concurrently aplies its operations to the
                // respective entries.
                // We therefore process all not-yet committed transactions to check whether any
                // operates on an entries that matches our query in the original version.

                // TODO: Is it a problem that the db-query of the entries and of the non-committed transactions are two distinct operations?
                //       Are there any race conditions?

                var transactions = LoadNonCommittedTransactionsAsync(cancellation);
                var transactionsEnumerator = transactions.GetEnumerator();

                try
                {
                    while (await transactionsEnumerator.MoveNext(cancellation))
                    {
                        var transaction = transactionsEnumerator.Current;

                        // We cannot check for Pending/AbortRequested, as the transactions may have transitioned into another state already.
                        Assert(transaction.Status != TransactionStatus.Initial && transaction.Status != TransactionStatus.Prepare);

                        var operations = transaction.Operations.Where(p => p.EntryType == typeof(TData));

                        foreach (var operation in operations)
                        {
                            Assert(operation.Entry is TData);

                            var id = DataPropertyHelper.GetId<TId, TData>(operation.Entry as TData);

                            // It is not neccessary to check entries of the id map because we already processed the complete id map.
                            if (_snapshots.ContainsKey(id))
                            {
                                continue;
                            }

                            var entry = await _entryStorage.GetEntryAsync(id, cancellation);

                            // If we cannot load the entry or its data, there are two possible cases:
                            // 1) The transaction creates the entry.
                            // 2) Another transaction deleted the entry.
                            // We may check for case (1) by comparand the expected version of the transaction operation to be zero.
                            // In case of (2), the transaction that deleted the entry is already committed. The case that it is contained in our list
                            // of non-committed transactions is handled separately.
                            // In either case, we can just skip the entry.
                            if (entry == null)
                            {
                                continue;
                            }

                            // We are in the recording state and therefore cannot have created an entry already.
                            Assert(entry.CreatingTransaction != _transaction.Id);

                            // Prevent phantom reads. We ignore all entries that were created of a transaction with a greater transaction id than our transaction's id.
                            if (entry.CreatingTransaction > _transaction.Id)
                            {
                                _nonCommittedTransactions.Add(transaction);
                                continue;
                            }

                            var snapshot = await GetCommittedSnapshotAsync(entry, translatedPredicate, checkEntry: true, cancellation);

                            if (snapshot == null)
                            {
                                continue;
                            }

                            Assert(snapshot.Data != null);
                            _snapshots.Add(snapshot.Id, snapshot);
                            _nonCommittedTransactions.Add(transaction);
                            await yield.Return(snapshot.Data);
                        }
                    }
                }
                finally
                {
                    transactionsEnumerator.Dispose();
                }

                return yield.Break();
            }

            private Expression<Func<IEntryState<TId, TData>, bool>> TranslateQuery(Expression<Func<IDataRepresentation<TId, TData>, bool>> query)
            {
                var parameter = Expression.Parameter(typeof(IEntryState<TId, TData>));
                var body = ParameterExpressionReplacer.ReplaceParameter(query.Body, query.Parameters.First(), parameter);
                return Expression.Lambda<Func<IEntryState<TId, TData>, bool>>(body, parameter);
            }

            private IAsyncEnumerable<Transaction> LoadNonCommittedTransactionsAsync(CancellationToken cancellation)
            {
                return _transactionManager.GetNonCommittedTransactionsAsync(cancellation).Concat(_nonCommittedTransactions.ToAsyncEnumerable()).Distinct();
            }

            // Get the latest committed snapshot for the specified entry that matches the predicate with respect to the set of non-committed transactions.
            // Walk back the history of operations on the entry till we find the latest committed state.
            private async ValueTask<IEntrySnapshot<TId, TData>> GetCommittedSnapshotAsync(IEntryState<TId, TData> entry,

                                                                                          Func<IDataRepresentation<TId, TData>, bool> predicate,
                                                                                          bool checkEntry,
                                                                                          CancellationToken cancellation)
            {
                Assert(entry != null);
                Assert(entry.Data != null);

                var committedTransactions = new List<Transaction>();
                var result = _entryStateTransformer.ToSnapshot(entry);
                Assert(result != null);

                if (checkEntry)
                {
                    result = result.MatchPredicate(predicate);
                }
#if DEBUG
                else
                {
                    Assert(predicate(result));
                }
#endif

                // PendingOperations are ordered by OriginalDataVersion
                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    var pendingOperation = entry.PendingOperations[i];
                    var originalData = pendingOperation.OriginalData;

                    // Load the transaction that issued the pending operation.
                    var transaction = await _transactionManager.GetTransactionAsync(pendingOperation.TransactionId, cancellation);

                    // The transaction that issued the pending operation is in our list of non-committed transaction.
                    // Even if the transaction is committed in the mean-time, we have to take the entry's data before the transaction committed
                    // in order guarantee atomicity of transactions.
                    if (_nonCommittedTransactions.Contains(transaction))
                    {
                        // Check whether originalData also matches our predicate.
                        // If either the transaction creates the entry (originalData.Data == 0) or
                        // the original data does not match our predicate, we return null.
                        result = originalData.MatchPredicate(predicate);

                        break;
                    }

                    await transaction.UpdateAsync(cancellation);
                    var transactionState = transaction.Status;

                    // If the transaction is not present or its state is committed,
                    // the transaction is already committed, it is just not removed from the collection.
                    if (!transactionState.IsCommitted())
                    {
                        // An initial transaction must not have pending operations.
                        Assert(transactionState != TransactionStatus.Initial);

                        // Check whether originalData also matches our predicate.
                        // If either the transaction creates the entry (originalData.Data == 0) or
                        // the original data does not match our predicate, we return null.
                        result = originalData.MatchPredicate(predicate);

                        if (transactionState == TransactionStatus.AbortRequested || transactionState == TransactionStatus.Aborted)
                        {
#if DEBUG
                            // After an operation thats transaction is aborted MUST NOT follow an operation that belongs to a committed transaction.
                            // The transaction commit operation must ensure this. 
                            // Because of this, it is the same case than the operation belonging to a pending transaction.

                            // We cannot just walk the operation list and check each transaction, as the transaction may have changed state in the mean-time
                            // rendering our operation list obsolete.

                            // TODO: Can we validate the condition by updating the operation list?

                            //for (var j = i + 1; j < entry.PendingOperations.Count; j++)
                            //{
                            //    var nextOperation = entry.PendingOperations[j];
                            //    var nextOperationTransaction = await _transactionManager.GetTransactionAsync(nextOperation.TransactionId, cancellation);

                            //    await nextOperationTransaction.UpdateAsync(cancellation);

                            //    // TODO: Assert failed nextOperationTransactionState == TransactionStatus.CleanedUp
                            //    Assert(nextOperationTransaction.Status != null &&
                            //           nextOperationTransaction.Status != TransactionStatus.CleanedUp &&
                            //           nextOperationTransaction.Status != TransactionStatus.Initial &&
                            //           nextOperationTransaction.Status != TransactionStatus.Committed);
                            //}
#endif

                            await AbortPendingOperationForTransactionAsync(entry, transaction, cancellation);
                        }

                        // We decided for the transaction to be non-committed now.
                        // We have to remember this decision for the future, even if the transaction commits in the mean-time.
                        _nonCommittedTransactions.Add(transaction);

                        break;
                    }

                    committedTransactions.Add(transaction);
                }

                var byIdSelector = DataPropertyHelper.BuildPredicate<TId, TData>(entry.Id).Compile();

                // Check all transaction in our list of non-committed transactions if they operate on the current entry.
                // If they do, we have to obtain the original data before the transaction was applied,
                // even if the transaction is committed in the mean-time, in order guarantee atomicity of transactions.
                foreach (var transaction in _nonCommittedTransactions)
                {
                    var operations = transaction.Operations;
                    Assert(!operations.Any(p => p == null || p.EntryType == null || p.Entry == null));

                    // TODO: ArgumentNullException occured for p
                    if (!operations.Any(p => AreEqual(p.EntryType, typeof(TData)) && byIdSelector((TData)p.Entry)))
                    {
                        continue;
                    }

                    // The transaction operates on the current entry. => Obtain the original data, before the transaction was applied.
                    // The transaction may already be aborted or committed and cleaned-up in the meantime.
                    // All pending operation belonging to the repspective transaction may have been removed.
                    // We do not known whether the transaction is aborted or committed in the described case and
                    // we cannot obtain the original data of the entry.
                    // We must abort the current transaction as we cannot ensure consistency.
                    // We cannot prevent this situation without disturbing the more major case that this relatively rare case
                    // does not occur but we can investigate with saving the original data when we store the transaction in the
                    // non-committed collection.
                    if (!entry.PendingOperations.Any(p => p.TransactionId == transaction.Id))
                    {
                        await _transaction.AbortAsync(cancellation);
                        throw new TransactionAbortedException();
                    }
                }

                // Commit the pending operations of all committed transactions.
                if (committedTransactions.Any())
                {
                    await CommitPendingOperationsForTransactionsAsync(entry, committedTransactions, cancellation);
                }

                return result;
            }

            private ValueTask<IEntryState<TId, TData>> CommitPendingOperationsForTransactionsAsync(IEntryState<TId, TData> entry,
                                                                                                   IEnumerable<Transaction> transactions,
                                                                                                   CancellationToken cancellation)
            {
                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.CommitAll(c, transactions.Select(p => p.Id));
                }

                return _entryStorage.UpdateEntryAsync(entry, Update, cancellation);
            }

            private async ValueTask<IEntryState<TId, TData>> AbortPendingOperationForTransactionAsync(IEntryState<TId, TData> entry,
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

            // TODO: Why does the == operator on the type objects not work here?
            private static bool AreEqual(Type left, Type right)
            {
                var result = left.IsAssignableFrom(right) && right.IsAssignableFrom(left);

#if DEBUG
                if (result)
                {
                    Assert(left == right);
                }
#endif

                return result;
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
