using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Transactions;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public sealed class Transaction : IEquatable<Transaction>, ITransaction
    {
        #region Fields

        private readonly ITransactionStateStorage _transactionStorage;
        private readonly ITransactionStateTransformer _transactionStateTransformer;
        private long? _id;
        private ITransactionState _entry;
        private int _entryVersion;
        private readonly object _entryLock = new object();
        private readonly object _idLock = new object();

        #endregion

        #region C'tor

        public Transaction(long id,
                           ITransactionStateStorage transactionStorage,
                           ITransactionStateTransformer transactionStateTransformer)
            : this(transactionStorage, transactionStateTransformer)
        {
            _id = id;
        }

        internal Transaction(ITransactionState entry,
                             ITransactionStateStorage transactionStorage,
                             ITransactionStateTransformer transactionStateTransformer)
            : this(transactionStorage, transactionStateTransformer)
        {
            Assert(entry != null);

            _entry = entry;
            _id = entry.Id;
        }

        public Transaction(ITransactionStateStorage transactionStorage,
                           ITransactionStateTransformer transactionStateTransformer)
        {
            if (transactionStorage == null)
                throw new ArgumentNullException(nameof(transactionStorage));

            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            _transactionStorage = transactionStorage;
            _transactionStateTransformer = transactionStateTransformer;
        }

        #endregion

        #region Properties

        private bool Exists
        {
            get
            {
                long? id;

                lock (_idLock)
                {
                    id = _id;
                }

                return id != null;
            }
        }

        public long Id
        {
            get
            {
                long? id;

                lock (_idLock)
                {
                    id = _id;
                }

                if (id == null)
                {
                    ThrowTransactionNotExistent();
                }

                return (long)id;
            }
        }

        #endregion

        #region State

        public async Task EnsureExistenceAsync(CancellationToken cancellation)
        {
            if (Exists)
                return;

            //ITransactionState entry;

            var id = await _transactionStorage.GetUniqueTransactionIdAsync(cancellation);
            var entry = _transactionStateTransformer.Create(id);
            var result = await _transactionStorage.CompareExchangeAsync(entry, null, cancellation);

            Assert(result);

            //var id = 0L;

            //do
            //{
            //    id = (await _transactionStorage.GetLatestTransactionAsync(id, cancellation))?.Id + 1 ?? 1;
            //    entry = _transactionStateTransformer.Create(id);
            //}
            //while (!await _transactionStorage.CompareExchangeAsync(entry, null, cancellation));

            lock (_idLock)
            {
                if (_id != null)
                {
                    // TODO: Remove transaction from store.
                    return;
                }

                _id = id;
            }

            lock (_entryLock)
            {
                if (_entryVersion < entry.Version)
                {
                    _entry = entry;
                    _entryVersion = entry.Version;
                }
            }
        }

        public Task<TransactionStatus?> GetStateAsync(CancellationToken cancellation)
        {
            return GetPropertyAsync(p => p.Status, default(TransactionStatus?), cancellation);
        }

        internal async Task DeleteTransactionAsync(CancellationToken cancellation)
        {
            var entry = await _transactionStorage.GetTransactionAsync(Id, cancellation);

            if (entry != null)
            {
#if DEBUG
                var state = ((ITransactionState)entry).Status;

                Assert(state == TransactionStatus.Aborted || state == TransactionStatus.CleanedUp);
#endif

                await _transactionStorage.RemoveAsync((ITransactionState)entry, cancellation);
            }

            lock (_entryLock)
            {
                _entry = null;
            }
        }

        public async Task<bool> TryPrepare(CancellationToken cancellation)
        {
            bool Condition(ITransactionState current)
            {
                return current.Status == TransactionStatus.Initial || current.Status == TransactionStatus.Prepare;
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Prepare(current), true);
            }

            if (!Exists)
            {
                return false;
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            if (entry == null)
            {
                ThrowTransactionNotExistent();
            }

            return success;
        }

        public async Task<bool> TryBeginCommitAsync(CancellationToken cancellation)
        {
            bool Condition(ITransactionState current)
            {
                return current.Status != TransactionStatus.Aborted &&
                       current.Status != TransactionStatus.AbortRequested;
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.BeginCommit(current), true);
            }

            if (!Exists)
            {
                return true;
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            if (entry == null)
            {
                ThrowTransactionNotExistent();
            }

            return success;
        }

        public async Task<bool> TryCommitAsync(CancellationToken cancellation)
        {
            bool Condition(ITransactionState current)
            {
                return current.Status != TransactionStatus.Aborted &&
                       current.Status != TransactionStatus.AbortRequested &&
                       current.Operations.All(p => p.State == OperationState.Applied);
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Commit(current), true);
            }

            if (!Exists)
            {
                return false;
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return entry != null && (success || ((ITransactionState)entry).Status.IsCommitted());
        }

        public async Task<bool> TryRequestAbortAsync(CancellationToken cancellation)
        {
            bool Condition(ITransactionState current)
            {
                return !current.Status.IsCommitted();
            }

            (ITransactionState, bool) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.RequestAbort(current), true);
            }

            if (!Exists)
            {
                return true;
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return entry == null || success;
        }

        public async Task AbortAsync(CancellationToken cancellation)
        {
            ITransactionState Update(ITransactionState current)
            {
                return _transactionStateTransformer.Abort(current);
            }

            if (!Exists)
            {
                return;
            }

            await UpdateEntry(Update, cancellation);
        }

        public async Task CleanUp(CancellationToken cancellation)
        {
            ITransactionState Update(ITransactionState current)
            {
                return _transactionStateTransformer.CleanUp(current);
            }

            if (!Exists)
            {
                return;
            }

            await UpdateEntry(Update, cancellation);
        }

        #endregion

        #region Operations

        public Task<ImmutableArray<IOperation>> GetOperationsAsync(CancellationToken cancellation)
        {
            return GetPropertyAsync(p => p.Operations, ImmutableArray<IOperation>.Empty, cancellation);
        }

        public ValueTask<ImmutableArray<IOperation>> GetOperationsFromCacheAsync(CancellationToken cancellation)
        {
            ITransactionState entry;

            lock (_entryLock)
            {
                entry = _entry;
            }

            if (entry != null)
            {
                return new ValueTask<ImmutableArray<IOperation>>(entry.Operations);
            }

            return new ValueTask<ImmutableArray<IOperation>>(GetPropertyAsync(p => p.Operations, ImmutableArray<IOperation>.Empty, cancellation));
        }

        public async Task<IOperation> StoreOperationAsync<TData>(TData data, int? expectedVersion, CancellationToken cancellation)
        {
            (ITransactionState, IOperation) Update(ITransactionState current)
            {
                var equality = DataPropertyHelper.CompilePredicate(data);
                var operationToReplace = current.Operations.FirstOrDefault(p => p.EntryType is TData comparand && equality(comparand));
                if (operationToReplace != null)
                {
                    current = _transactionStateTransformer.RemoveOperation(current, operationToReplace);
                }
                var desired = _transactionStateTransformer.Store(current, data, expectedVersion, out var operation);
                return (desired, operation);
            }

            // No condition needed here because a transaction not beeing in initial state
            // will throw an invalid operation exception which is the correct behavior.
            await EnsureExistenceAsync(cancellation);

            var (entry, result) = await UpdateEntry(Update, cancellation);

            if (entry == null)
            {
                ThrowTransactionNotExistent();
            }

            Assert(result != null);

            return result;
        }

        // TODO: If we delete a non existing entry, or creating an entry and delete it afterwards, this should be a no op.
        public async Task<IOperation> DeleteOperationAsync<TData>(TData data, int? expectedVersion, CancellationToken cancellation)
        {
            (ITransactionState, IOperation) Update(ITransactionState current)
            {
                var equality = DataPropertyHelper.CompilePredicate(data);
                var operationToReplace = current.Operations.FirstOrDefault(p => p.EntryType is TData comparand && equality(comparand));
                if (operationToReplace != null)
                {
                    current = _transactionStateTransformer.RemoveOperation(current, operationToReplace);
                }
                var desired = _transactionStateTransformer.Delete(current, data, expectedVersion, out var operation);
                return (desired, operation);
            }

            // No condition needed here because a transaction not beeing in initial state
            // will throw an invalid operation exception which is the correct behavior.
            await EnsureExistenceAsync(cancellation);

            var (entry, result) = await UpdateEntry(Update, cancellation);

            if (entry == null)
            {
                ThrowTransactionNotExistent();
            }

            Assert(result != null);

            return result;
        }

        public async Task<bool> TryApplyOperationAsync(IOperation operation, CancellationToken cancellation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            CheckExistence();

            bool Condition(ITransactionState current)
            {
                // We must include the committed state here because otherwise sucess will be false and the caller asumes the transaction was aborted.
                // This is not problematic as a concurrently committed transaction leading our operation to be a nop.
                // We do not include the CleanedUp state with reasoning. See: TransactionalStore.ApplyOperationsAsync()
                return current.Status == TransactionStatus.Pending || current.Status == TransactionStatus.Committed;
            }

            (ITransactionState entry, bool success) Update(ITransactionState current)
            {
                return (_transactionStateTransformer.Apply(current, operation), true);
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            if (entry == null)
            {
                ThrowTransactionNotExistent();
            }

            return success;
        }

        public async Task<bool> TryUnapplyOperationAsync(IOperation operation, CancellationToken cancellation)
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

            if (!Exists)
            {
                return false;
            }

            var (entry, success) = await UpdateEntry(Update, Condition, cancellation);

            return success;
        }

        #endregion

        private async Task<T> GetPropertyAsync<T>(Func<ITransactionState, T> selector, T defaultValue, CancellationToken cancellation)
        {
            long? id;

            lock (_idLock)
            {
                id = _id;
            }

            if (id == null)
            {
                return default;
            }

            var entry = await _transactionStorage.GetTransactionAsync((long)id, cancellation);

            if (entry == null)
            {
                return defaultValue;
            }

            var result = selector(entry);

            Assert(entry != null);

            lock (_entryLock)
            {
                if (_entryVersion < entry.Version)
                {
                    _entry = entry;
                    _entryVersion = entry.Version;
                }
            }

            return result;
        }

        private void CheckExistence()
        {
            if (!Exists)
            {
                ThrowTransactionNotExistent();
            }
        }

        private static void ThrowTransactionNotExistent()
        {
            throw new InvalidOperationException("Cannot operate on a non-existing transaction.");
        }

        private Task<ITransactionState> UpdateEntry(Func<ITransactionState, ITransactionState> update,
                                                    CancellationToken cancellation)
        {
            return UpdateEntry(update, condition: entry => true, cancellation);
        }

        private async Task<ITransactionState> UpdateEntry(Func<ITransactionState, ITransactionState> update,
                                                          Func<ITransactionState, bool> condition,
                                                          CancellationToken cancellation)
        {
            Assert(update != null);
            Assert(condition != null);

            long? id;

            lock (_idLock)
            {
                id = _id;
            }

            if (id == null)
            {
                return null;
            }

            ITransactionState entry;

            lock (_entryLock)
            {
                entry = _entry;
            }

            if (entry == null)
            {
                entry = await _transactionStorage.GetTransactionAsync((long)id, cancellation);

                if (entry == null)
                {
                    return null;
                }
            }

            while (condition(entry))
            {
                var desired = update(entry);

                if (desired == entry)
                {
                    break;
                }

                if (await _transactionStorage.CompareExchangeAsync(desired, entry, cancellation))
                {
                    entry = desired;
                    break;
                }

                entry = await _transactionStorage.GetTransactionAsync((long)id, cancellation);

                if (entry == null)
                {
                    return null;
                }
            }

            Assert(entry != null);

            lock (_entryLock)
            {
                if (_entryVersion < ((ITransactionState)entry).Version)
                {
                    _entry = entry;
                    _entryVersion = ((ITransactionState)entry).Version;
                }
            }

            return entry;
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

            long id;

            lock (_idLock)
            {
                if (_id == null)
                {
                    return default;
                }

                id = (long)_id;
            }

            ITransactionState entry;
            ITransactionState desired;

            lock (_entryLock)
            {
                entry = _entry;
            }

            if (entry == null)
            {
                entry = await _transactionStorage.GetTransactionAsync(id, cancellation);

                if (entry == null)
                {
                    return default;
                }
            }

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

                entry = await _transactionStorage.GetTransactionAsync(id, cancellation);

                if (entry == null)
                {
                    return (null, default);
                }
            }

            Assert(entry != null);

            lock (_entryLock)
            {
                if (_entryVersion < entry.Version)
                {
                    _entry = entry;
                    _entryVersion = entry.Version;
                }
            }

            return (entry, ret);
        }

        public override bool Equals(object obj)
        {
            return obj is Transaction transaction && Equals(transaction);
        }

        public bool Equals(Transaction other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(other, this))
                return true;

            long? id1, id2;

            lock (_idLock)
            {
                id1 = _id;
            }

            lock (other._idLock)
            {
                id2 = other._id;
            }

            if (id1 == null || id2 == null)
                return false;

            return id1.Value == id2.Value;
        }

        public override int GetHashCode()
        {
            long? id;

            lock (_idLock)
            {
                id = _id;
            }

            return id.GetHashCode();
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
    }
}
