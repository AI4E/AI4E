using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using AI4E.Utils.AsyncEnumerable;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Transactions
{
    public sealed class EntryStateStorage<TId, TData> : IEntryStateStorage<TId, TData>
        where TData : class
    {
        private readonly IFilterableDatabase _database;

        public EntryStateStorage(IFilterableDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        public Task<bool> CompareExchangeAsync(IEntryState<TId, TData> entry, IEntryState<TId, TData> comparand, CancellationToken cancellation = default)
        {
            return _database.CompareExchangeAsync(AsStoredEntry(entry), AsStoredEntry(comparand), p => p.Version, cancellation);
        }

        public IAsyncEnumerable<IEntryState<TId, TData>> GetEntriesAsync(Expression<Func<IEntryState<TId, TData>, bool>> predicate, CancellationToken cancellation = default)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            return _database.GetAsync(TranslatePredicate(predicate), cancellation);
        }

        private Expression<Func<StoredEntry, bool>> TranslatePredicate(Expression<Func<IEntryState<TId, TData>, bool>> predicate)
        {
            Assert(predicate != null);

            var parameter = Expression.Parameter(typeof(StoredEntry));
            var body = ParameterExpressionReplacer.ReplaceParameter(predicate.Body, predicate.Parameters.First(), parameter);

            return Expression.Lambda<Func<StoredEntry, bool>>(body, parameter);

        }

        private static StoredEntrySnapshot AsStoredEntrySnapshot(IEntrySnapshot<TId, TData> entrySnapshot)
        {
            if (entrySnapshot == null)
            {
                return null;
            }

            if (entrySnapshot is StoredEntrySnapshot storedEntrySnapshot)
            {
                return storedEntrySnapshot;
            }

            return new StoredEntrySnapshot(entrySnapshot);
        }

        private static StoredPendingOperation AsStoredPendingOperation(IPendingOperation<TId, TData> pendingOperation)
        {
            if (pendingOperation == null)
                return null;

            if (pendingOperation is StoredPendingOperation storedPendingOperation)
            {
                return storedPendingOperation;
            }

            return new StoredPendingOperation(pendingOperation);
        }

        private static StoredEntry AsStoredEntry(IEntryState<TId, TData> entry)
        {
            if (entry == null)
                return null;

            if (entry is StoredEntry storedEntry)
            {
                return storedEntry;
            }

            return new StoredEntry(entry);
        }

        private sealed class StoredEntry : IEntryState<TId, TData>
        {
            public StoredEntry(IEntryState<TId, TData> entry)
            {
                DataVersion = entry.DataVersion;
                LastWriteTime = entry.LastWriteTime;
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                CreatingTransaction = entry.CreatingTransaction;
                PendingOperations = new List<StoredPendingOperation>(entry.PendingOperations.Select(p => AsStoredPendingOperation(p)));

                for (var i = 0; i < entry.PendingOperations.Count; i++)
                {
                    Assert(entry.PendingOperations[i].TransactionId == PendingOperations[i].TransactionId);
                }

                PendingTransactions = new List<long>(entry.PendingTransactions);

                Id = entry.Id;
                Data = entry.Data;
            }

            private StoredEntry()
            {
                PendingOperations = new List<StoredPendingOperation>();
                PendingTransactions = new List<long>();
            }

            public int DataVersion { get; private set; }

            public DateTime LastWriteTime { get; private set; }

            public int Version { get; private set; }

            public DateTime CreationTime { get; private set; }

            public long CreatingTransaction { get; private set; }

            public List<StoredPendingOperation> PendingOperations { get; private set; }

            public List<long> PendingTransactions { get; set; }

            ImmutableList<IPendingOperation<TId, TData>> IEntryState<TId, TData>.PendingOperations => PendingOperations.Cast<IPendingOperation<TId, TData>>().ToImmutableList();
            ImmutableList<long> IEntryState<TId, TData>.PendingTransactions => PendingTransactions.ToImmutableList();

            public TId Id { get; private set; }

            public TData Data { get; private set; }
        }

        private sealed class StoredPendingOperation : IPendingOperation<TId, TData>
        {
            public StoredPendingOperation(IPendingOperation<TId, TData> pendingOperation)
            {
                Assert(pendingOperation != null);

                TransactionId = pendingOperation.TransactionId;
                OriginalData = AsStoredEntrySnapshot(pendingOperation.OriginalData);
                OperationTime = pendingOperation.OperationTime;
            }

            private StoredPendingOperation() { }

            public long TransactionId { get; private set; }

            public StoredEntrySnapshot OriginalData { get; private set; }

            IEntrySnapshot<TId, TData> IPendingOperation<TId, TData>.OriginalData => OriginalData;

            public DateTime OperationTime { get; private set; }
        }

        private sealed class StoredEntrySnapshot : IEntrySnapshot<TId, TData>
        {
            public StoredEntrySnapshot(IEntrySnapshot<TId, TData> entrySnapshot)
            {
                Assert(entrySnapshot != null);

                DataVersion = entrySnapshot.DataVersion;
                LastWriteTime = entrySnapshot.LastWriteTime;
                Id = entrySnapshot.Id;
                Data = entrySnapshot.Data;
            }

            private StoredEntrySnapshot() { }

            public int DataVersion { get; private set; }

            public DateTime? LastWriteTime { get; private set; }

            public TId Id { get; private set; }

            public TData Data { get; private set; }
        }
    }

    public sealed class EntryStateStorageFactory : IEntryStateStorageFactory
    {
        private readonly IFilterableDatabase _dataStore;

        public EntryStateStorageFactory(IFilterableDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _dataStore = database;
        }

        public IEntryStateStorage<TId, TData> GetEntryStorage<TId, TData>()
            where TData : class
        {
            return new EntryStateStorage<TId, TData>(_dataStore);
        }
    }

    public static class EntryStateStorageExtension
    {
        public static async ValueTask<IEntryState<TId, TData>> GetEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                               TId id,
                                                                               CancellationToken cancellation = default)
            where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            var entries = await entryStorage.GetEntriesAsync(DataPropertyHelper.BuildPredicate<TId, IEntryState<TId, TData>>(id), cancellation);

            return entries.FirstOrDefault();
        }

        public static async ValueTask<IEntryState<TId, TData>> GetEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                               Expression<Func<IEntryState<TId, TData>, bool>> predicate,
                                                                               CancellationToken cancellation = default)
             where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var entries = await entryStorage.GetEntriesAsync(predicate, cancellation);

            return entries.FirstOrDefault();
        }

        public static async ValueTask<IEntryState<TId, TData>> GetEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                               IEntryState<TId, TData> comparand,
                                                                               CancellationToken cancellation = default)
             where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));
            if (comparand == null)
                throw new ArgumentNullException(nameof(comparand));

            var entries = await entryStorage.GetEntriesAsync(DataPropertyHelper.BuildPredicate(comparand), cancellation);

            return entries.FirstOrDefault();
        }

        #region UpdateEntry

        public static ValueTask<IEntryState<TId, TData>> UpdateEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                       IEntryState<TId, TData> entry,
                                                                       Func<IEntryState<TId, TData>, IEntryState<TId, TData>> update,
                                                                       CancellationToken cancellation) where TData : class
        {
            return UpdateEntryAsync(entryStorage, entry, update, condition: e => true, cancellation);
        }

        public static async ValueTask<IEntryState<TId, TData>> UpdateEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                             IEntryState<TId, TData> entry,
                                                                             Func<IEntryState<TId, TData>, IEntryState<TId, TData>> update,
                                                                             Func<IEntryState<TId, TData>, bool> condition,
                                                                             CancellationToken cancellation) where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (update == null)
                throw new ArgumentNullException(nameof(update));

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            var id = entry.Id;

            while (condition(entry))
            {
                var desired = update(entry);

                if (desired == entry)
                {
                    return entry;
                }

                if (await entryStorage.CompareExchangeAsync(desired, entry, cancellation))
                {
                    return entry = desired;
                }

                entry = await entryStorage.GetEntryAsync(id, cancellation);

                if (entry == null)
                {
                    return null;
                }
            }

            return entry;
        }

        public static ValueTask<(IEntryState<TId, TData> entry, T result)> UpdateEntryAsync<TId, TData, T>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                                            IEntryState<TId, TData> entry,
                                                                                            Func<IEntryState<TId, TData>, (IEntryState<TId, TData> entry, T result)> update,
                                                                                            CancellationToken cancellation) where TData : class
        {
            return UpdateEntryAsync(entryStorage, entry, update, condition: e => true, cancellation);
        }

        public static async ValueTask<(IEntryState<TId, TData> entry, T result)> UpdateEntryAsync<TId, TData, T>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                                                  IEntryState<TId, TData> entry,
                                                                                                  Func<IEntryState<TId, TData>, (IEntryState<TId, TData> entry, T result)> update,
                                                                                                  Func<IEntryState<TId, TData>, bool> condition,
                                                                                                  CancellationToken cancellation) where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (update == null)
                throw new ArgumentNullException(nameof(update));

            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            var id = entry.Id;

            while (condition(entry))
            {
                var (desired, result) = update(entry);

                if (desired == entry)
                {
                    return (entry, result);
                }

                if (await entryStorage.CompareExchangeAsync(desired, entry, cancellation))
                {
                    return (entry = desired, result);
                }

                entry = await entryStorage.GetEntryAsync(id, cancellation);

                if (entry == null)
                {
                    return (null, default);
                }
            }

            return (entry, default);
        }

        public static async ValueTask<IEntryState<TId, TData>> UpdateEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                       Expression<Func<IEntryState<TId, TData>, bool>> predicate,
                                                                       Func<IEntryState<TId, TData>, IEntryState<TId, TData>> update,
                                                                       CancellationToken cancellation) where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var entry = await entryStorage.GetEntryAsync(predicate, cancellation);

            if (entry == null)
                return null;

            return await UpdateEntryAsync(entryStorage, entry, update, cancellation);
        }

        public static async ValueTask<IEntryState<TId, TData>> UpdateEntryAsync<TId, TData>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                             Expression<Func<IEntryState<TId, TData>, bool>> predicate,
                                                                             Func<IEntryState<TId, TData>, IEntryState<TId, TData>> update,
                                                                             Func<IEntryState<TId, TData>, bool> condition,
                                                                             CancellationToken cancellation) where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var entry = await entryStorage.GetEntryAsync(predicate, cancellation);

            if (entry == null)
                return null;

            return await UpdateEntryAsync(entryStorage, entry, update, condition, cancellation);
        }

        public static async ValueTask<(IEntryState<TId, TData> entry, T result)> UpdateEntryAsync<TId, TData, T>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                                                  Expression<Func<IEntryState<TId, TData>, bool>> predicate,
                                                                                                  Func<IEntryState<TId, TData>, (IEntryState<TId, TData> entry, T result)> update,
                                                                                                  CancellationToken cancellation) where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var entry = await entryStorage.GetEntryAsync(predicate, cancellation);

            if (entry == null)
                return (null, default);

            return await UpdateEntryAsync(entryStorage, entry, update, cancellation);
        }

        public static async ValueTask<(IEntryState<TId, TData> entry, T result)> UpdateEntryAsync<TId, TData, T>(this IEntryStateStorage<TId, TData> entryStorage,
                                                                                                  Expression<Func<IEntryState<TId, TData>, bool>> predicate,
                                                                                                  Func<IEntryState<TId, TData>, (IEntryState<TId, TData> entry, T result)> update,
                                                                                                  Func<IEntryState<TId, TData>, bool> condition,
                                                                                                  CancellationToken cancellation) where TData : class
        {
            if (entryStorage == null)
                throw new ArgumentNullException(nameof(entryStorage));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var entry = await entryStorage.GetEntryAsync(predicate, cancellation);

            if (entry == null)
                return (null, default);

            return await UpdateEntryAsync(entryStorage, entry, update, condition, cancellation);
        }

        #endregion
    }
}
