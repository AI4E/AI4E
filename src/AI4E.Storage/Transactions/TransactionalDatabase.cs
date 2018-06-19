using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public sealed class TransactionalDatabase : ITransactionalDatabase
    {
        private readonly ConcurrentDictionary<Type, ITypedTransactionalStore> _typedStores = new ConcurrentDictionary<Type, ITypedTransactionalStore>();

        private readonly ITransactionManager _transactionManager;
        private readonly IEntryStateTransformerFactory _entryStateTransformerFactory;
        private readonly IEntryStateStorageFactory _entryStorageFactory;
        private readonly ILogger<TransactionalDatabase> _logger;
        private readonly ITransaction _transaction;
        private readonly AsyncLock _lock = new AsyncLock();

        // We need to remember all transactions for which we decided that it is not committed and we therefore not include its operations. 
        // That means we have a look at the database state BEFORE the transaction committed or aborted.
        // If we decided once for that, we have to take the same decision always to ensure transaction atomicity.
        private readonly ISet<ITransaction> _nonCommittedTransactions = new HashSet<ITransaction>();

        public TransactionalDatabase(ITransactionManager transactionManager,
                         IEntryStateTransformerFactory entryManagerFactory,
                         IEntryStateStorageFactory entryStorageFactory,
                         ILogger<TransactionalDatabase> logger)
        {
            if (transactionManager == null)
                throw new ArgumentNullException(nameof(transactionManager));

            if (entryManagerFactory == null)
                throw new ArgumentNullException(nameof(entryManagerFactory));

            if (entryStorageFactory == null)
                throw new ArgumentNullException(nameof(entryStorageFactory));

            _transactionManager = transactionManager;
            _entryStateTransformerFactory = entryManagerFactory;
            _entryStorageFactory = entryStorageFactory;
            _logger = logger;
            _transaction = transactionManager.CreateTransaction();
        }

        public Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return GetTypedStore<TData>().StoreAsync(data, cancellation);
        }

        public Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
            where TData : class
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return GetTypedStore<TData>().RemoveAsync(data, cancellation);
        }

        public Task<IEnumerable<TData>> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
            where TData : class
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return GetTypedStore<TData>().GetAsync(predicate, cancellation).AsTask();
        }

        public async Task<bool> TryCommitAsync(CancellationToken cancellation)
        {
            await PrepareAsync(cancellation);

            return await _transactionManager.ProcessTransactionAsync(_transaction, cancellation) == ProcessingState.Committed;
        }

        public async Task RollbackAsync(CancellationToken cancellation)
        {
            if (await _transaction.TryRequestAbortAsync(cancellation))
            {
                await _transactionManager.ProcessTransactionAsync(_transaction, cancellation);
            }
        }

        private async Task PrepareAsync(CancellationToken cancellation)
        {
            // The commit operation must not be called concurrently. 
            // Otherwise it is possible that transactions are added as pending transactions to entry after the transaction committed 
            // or abort is requested and its pending operations are removed leading to various unconcistencies.
            using (await _lock.LockAsync(cancellation))
            {
                if (await _transaction.TryPrepare(cancellation))
                {
                    var operations = await _transaction.GetOperationsAsync(cancellation);

                    foreach (var operation in operations.Shuffle())
                    {
                        // Add transaction to pending transactions
                        var typedStore = GetTypedStore(operation.EntryType);

                        await typedStore.AddPendingTransactionAsync(operation.Entry, cancellation);
                    }
                }

                await _transaction.TryBeginCommitAsync(cancellation);
            }
        }

        private ITypedTransactionalStore GetTypedStore(Type dataType)
        {
            return _typedStores.GetOrAdd(dataType, CreateTypedStore);
        }

        private ITypedTransactionalStore<TData> GetTypedStore<TData>()
            where TData : class
        {
            return (ITypedTransactionalStore<TData>)_typedStores.GetOrAdd(typeof(TData), CreateTypedStore);
        }

        private ITypedTransactionalStore CreateTypedStore(Type dataType)
        {
            Assert(dataType != null);

            var idType = DataPropertyHelper.GetIdType(dataType);

            var typedStore = Activator.CreateInstance(typeof(TypedTransactionalStore<,>).MakeGenericType(idType, dataType), this);

            return (ITypedTransactionalStore)typedStore;
        }

        private interface ITypedTransactionalStore
        {
            Task AddPendingTransactionAsync(object data, CancellationToken cancellation);
        }

        private interface ITypedTransactionalStore<TData> : ITypedTransactionalStore
            where TData : class
        {
            Task StoreAsync(TData data, CancellationToken cancellation);
            Task RemoveAsync(TData data, CancellationToken cancellation);
            ValueTask<IEnumerable<TData>> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation);
        }

        private sealed class TypedTransactionalStore<TId, TData> : ITypedTransactionalStore<TData>
            where TData : class
        {
            private readonly TransactionalDatabase _dataStore;
            private readonly ITransactionManager _transactionManager;
            private readonly IEntryStateStorage<TId, TData> _entryStateStorage;
            private readonly IEntryStateTransformer<TId, TData> _entryStateTransformer;
            private readonly Dictionary<TId, IEntrySnapshot<TId, TData>> _identityMap;
            private readonly ISet<ITransaction> _nonCommittedTransactions;

            public TypedTransactionalStore(TransactionalDatabase transactionalStore)
            {
                Assert(transactionalStore != null);

                _dataStore = transactionalStore;

                _transactionManager = _dataStore._transactionManager;
                _nonCommittedTransactions = _dataStore._nonCommittedTransactions;
                _entryStateTransformer = _dataStore._entryStateTransformerFactory.GetEntryManager<TId, TData>();
                _entryStateStorage = _dataStore._entryStorageFactory.GetEntryStorage<TId, TData>();

                Assert(_transactionManager != null);
                Assert(_nonCommittedTransactions != null);
                Assert(_entryStateTransformer != null);
                Assert(_entryStateStorage != null);

                var idEqualityComparer = new IdEqualityComparer<TId>();
                _identityMap = new Dictionary<TId, IEntrySnapshot<TId, TData>>(idEqualityComparer);
            }

            public ITransaction Transaction => _dataStore._transaction;

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

            private Task AddPendingTransactionAsync(TData data, CancellationToken cancellation)
            {
                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var predicate = DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id, p => p.Id);

                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.AddPendingTransaction(c, Transaction.Id);
                }

                bool Condition(IEntryState<TId, TData> c)
                {
                    return !c.PendingTransactions.Contains(Transaction.Id);
                }

                return _entryStateStorage.UpdateEntryAsync(predicate, Update, Condition, cancellation).AsTask();
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

            public async Task StoreAsync(TData data, CancellationToken cancellation = default)
            {
                Assert(data != null);

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entryPresent = _identityMap.TryGetValue(id, out var entry);
                var expectedVersion = GetExpectedVersion(entryPresent ? entry : null);
                UpdateIdMap(data, id, entryPresent, entry);

                try
                {
                    await Transaction.StoreOperationAsync(data, expectedVersion, cancellation);
                }
                catch
                {
                    if (entryPresent)
                    {
                        Assert(entry.Data != null);
                        _identityMap[id] = entry;
                    }
                    else
                    {
                        _identityMap.Remove(id);
                    }

                    throw;
                }
            }

            public async Task RemoveAsync(TData data, CancellationToken cancellation = default)
            {
                Assert(data != null);

                var id = DataPropertyHelper.GetId<TId, TData>(data);
                var entryPresent = _identityMap.TryGetValue(id, out var entry);
                var expectedVersion = GetExpectedVersion(entryPresent ? entry : null);
                UpdateIdMap(data, id, entryPresent, entry);

                try
                {
                    await Transaction.DeleteOperationAsync(data, expectedVersion, cancellation);
                }
                catch
                {
                    if (entryPresent)
                    {
                        Assert(entry.Data != null);
                        _identityMap[id] = entry;
                    }
                    else
                    {
                        _identityMap.Remove(id);
                    }

                    throw;
                }
            }

            private void UpdateIdMap(TData data, TId id, bool entryPresent, IEntrySnapshot<TId, TData> entry)
            {
                _identityMap[id] = GetIdMapEntry(data, id, entryPresent, entry);
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

            public async ValueTask<IEnumerable<TData>> GetAsync(Expression<Func<TData, bool>> predicate, CancellationToken cancellation)
            {
                Assert(predicate != null);

                await Transaction.EnsureExistenceAsync(cancellation);

                var lamda = BuildPredicate(predicate);
                var queryExpression = BuildQueryExpression(predicate);
                var compiledLambda = lamda.Compile();

                var idEqualityComparer = new IdEqualityComparer<TId>();
                var result = new Dictionary<TId, IEntrySnapshot<TId, TData>>(idEqualityComparer);

                var entries = await _entryStateStorage.GetEntriesAsync(queryExpression, cancellation);
                await ProcessEntriesAsync(entries, result, compiledLambda, cancellation);
                await ProcessNonCommittedTransactionsAsync(compiledLambda, result, cancellation);

                return result.Values.Select(p => p.Data);
            }

            // There may be entries that match the predicate but are not included in the result set because 
            // one / several transactions modified the entry but the transactions are not fully committed yet and the entry DID NOT MATCH the predicate.
            // We have to check whether the entry matches the predicate without the changes that the transactions produced to prevent a dirty read.
            // Therefore we have to look for all pending / request-aborted transactions that modified at least one entry of type 'TData'. 
            private async Task ProcessNonCommittedTransactionsAsync(Func<IDataRepresentation<TId, TData>, bool> compiledLambda,
                                                                    IDictionary<TId, IEntrySnapshot<TId, TData>> result,
                                                                    CancellationToken cancellation)
            {
                var transactions = await LoadNonCommittedTransactionsAsync(cancellation);

                foreach (var transaction in transactions)
                {
                    var operations = (await transaction.GetOperationsAsync(cancellation)).Where(p => p.EntryType == typeof(TData));

                    foreach (var operation in operations)
                    {
                        Assert(operation.Entry is TData);

                        var id = DataPropertyHelper.GetId<TId, TData>(operation.Entry as TData);

                        if (result.ContainsKey(id))
                        {
                            continue;
                        }

                        // It is not neccessary to check entries of the id map because ProcessEntriesAsync already processed the complete id map.
                        if (!_identityMap.TryGetValue(id, out _))
                        {
                            var entry = await _entryStateStorage.GetEntryAsync(id, cancellation);

                            Assert(entry != null);

                            if (entry.CreatingTransaction < Transaction.Id)
                            {
                                // TODO: Do we need to reload added transactions?
                                var snapshot = await GetCommittedSnapshotAsync(entry, compiledLambda, cancellation);

                                if (snapshot != null)
                                {
                                    result.TryAdd(id, snapshot);
                                }
                            }
                        }
                    }
                }
            }

            private async Task<IEnumerable<ITransaction>> LoadNonCommittedTransactionsAsync(CancellationToken cancellation)
            {
                _nonCommittedTransactions.UnionWith(await _transactionManager.GetNonCommittedTransactionsAsync(cancellation));
                return _nonCommittedTransactions;
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
                var transactionComparison = Expression.LessThan(creationTransaction, Expression.Constant(Transaction.Id));

                var dataAccessor = GetDataAccessor();
                var data = ParameterExpressionReplacer.ReplaceParameter(dataAccessor.Body, dataAccessor.Parameters.First(), parameter);
                var isDeleted = Expression.ReferenceEqual(data, Expression.Constant(null, typeof(TData)));
                var body = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), data);
                return Expression.Lambda<Func<IEntryState<TId, TData>, bool>>(Expression.AndAlso(Expression.Not(isDeleted),Expression.AndAlso(transactionComparison, body)), parameter);
            }

            private static Expression<Func<IEntryState<TId, TData>, long>> GetCreationTransactionAccessor()
            {
                return p => p.CreatingTransaction;
            }

            private static Expression<Func<IEntryState<TId, TData>, TData>> GetDataAccessor()
            {
                return p => p.Data;
            }

            private async Task ProcessEntriesAsync(IEnumerable<IEntryState<TId, TData>> entries,
                                                   IDictionary<TId, IEntrySnapshot<TId, TData>> resultSet,

                                                   Func<IDataRepresentation<TId, TData>, bool> predicate,
                                                   CancellationToken cancellation)
            {
                // This is not parallelized with Task.WhenAll with care.
                foreach (var entry in entries)
                {
                    var snapshot = await ProcessEntryAsync(entry, predicate, cancellation);

                    if (snapshot == null)
                        continue;

                    resultSet.TryAdd(snapshot.Id, snapshot);
                }

                // Prevent phantom reads when other transactions deleted an entry.
                foreach (var entry in _identityMap.Where(p => predicate(p.Value)))
                {
                    resultSet.TryAdd(entry.Key, entry.Value);
                }
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
                if (_identityMap.ContainsKey(entry.Id))
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

                    _identityMap.Add(result.Id, result);
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
                    var transaction = _transactionManager.GetTransaction(pendingOperation.TransactionId);

                    if (_nonCommittedTransactions.Contains(transaction))
                    {
                        result = predicate(pendingOperation.OriginalData) ? pendingOperation.OriginalData : null;
                        break;
                    }

                    var transactionState = await transaction.GetStateAsync(cancellation);

                    // If the transaction is not present or its state is committed, the transaction is already committed, it is just not removed from the collection.
                    if (!transactionState.IsCommitted())
                    {
                        // An initial transaction must not have pending operations.
                        Assert(transactionState != TransactionStatus.Initial);

                        var originalData = pendingOperation.OriginalData;

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
                                var nextOperationTransaction = _transactionManager.GetTransaction(nextOperation.TransactionId);

                                var nextOperationTransactionState = await nextOperationTransaction.GetStateAsync(cancellation);

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
                    var operations = await transaction.GetOperationsFromCacheAsync(cancellation);

                    // TODO: Why does the == on the type objects does not work here?
                    /*p.EntryType == typeof(TData)*/
                    if (!operations.Any(p => AreEqual(p.EntryType, typeof(TData)) && byIdSelector(p)))
                        continue;

                    // The transaction is either non-committed or viewed as non-committed by the current transaction.
                    // If the operation aborts or completed in the meantime, all pending operation belongin to the repspective
                    // transaction may be removed. We do not known whether the transaction is aborted or committed in the described case and
                    // We cannot obtain the original data of the entry.
                    // We must abort the current transaction as we cannot ensure consistency.
                    // We cannot prevent this situation without disturbing the more major case that this relatively rare case
                    // does not occur but we can investigate with saving the original data when we save the transaction in the
                    // non-committed collection.
                    if (!entry.PendingOperations.Any(p => p.TransactionId == Transaction.Id))
                    {
                        await _dataStore.RollbackAsync(cancellation);
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

                return _entryStateStorage.UpdateEntryAsync(entry, Update, cancellation);
            }

            private async ValueTask<IEntryState<TId, TData>> AbortAsync(IEntryState<TId, TData> entry,
                                                              ITransaction transaction,
                                                              CancellationToken cancellation)
            {
                IEntryState<TId, TData> Update(IEntryState<TId, TData> c)
                {
                    return _entryStateTransformer.Abort(c, transaction.Id);
                }

                entry = await _entryStateStorage.UpdateEntryAsync(entry, Update, cancellation);

                Assert(!entry.PendingOperations.Any(p => p.TransactionId == transaction.Id));

                return entry;
            }

            private static Expression<Func<IDataRepresentation<TId, TData>, bool>> BuildPredicate(Expression<Func<TData, bool>> predicate)
            {
                Expression<Func<IDataRepresentation<TId, TData>, TData>> dataSelector = (entry => entry.Data);
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
    }
}
