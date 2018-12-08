using System;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Internal;
using AI4E.Utils;
using static System.Diagnostics.Debug;
using static AI4E.Utils.DebugEx;

namespace AI4E.Storage.Transactions
{
    public sealed class TransactionStateTransformer : ITransactionStateTransformer
    {
        private static readonly ImmutableArray<Operation> _noOperations = ImmutableArray<Operation>.Empty;

        public ITransactionState Create(long id)
        {
            return new TransactionState(id, version: 1, TransactionStatus.Initial, _noOperations);
        }

        private Operation ToOperation(IOperation operation)
        {
            if (operation == null)
                return null;

            if (operation is Operation result)
                return result;

            return new Operation(operation.TransactionId,
                                 operation.Id,
                                 operation.OperationType,
                                 operation.EntryType,
                                 operation.Entry,
                                 operation.ExpectedVersion,
                                 operation.State);
        }

        private TransactionState ToTransactionState(ITransactionState transactionState)
        {
            if (transactionState == null)
                return null;

            if (transactionState is TransactionState result)
                return result;

            return new TransactionState(transactionState.Id,
                                        transactionState.Version,
                                        transactionState.Status,
                                        transactionState.Operations.Select(p => ToOperation(p)).ToImmutableArray());
        }

        private sealed class Operation : IOperation
        {
            #region C'tor

            public Operation(long transactionId,
                             long id,
                             OperationType operationType,
                             Type entryType,
                             object entry,
                             int? expectedVersion,
                             OperationState state)
            {
                ValidateArgs(operationType, entryType, entry, expectedVersion);

                TransactionId = transactionId;
                Id = id;
                Entry = entry;
                EntryType = entryType;
                ExpectedVersion = expectedVersion;
                OperationType = operationType;
                State = state;
            }

            public long TransactionId { get; }
            public long Id { get; }
            public object Entry { get; }
            public Type EntryType { get; }
            public int? ExpectedVersion { get; }
            public OperationType OperationType { get; }
            public OperationState State { get; }

            private static void ValidateArgs(OperationType operationType, Type entryType, object entry, int? expectedVersion)
            {
                if (!operationType.IsValid())
                    throw new ArgumentException($"The argument must be one of the values defined in {typeof(OperationType).FullName}.", nameof(operationType));

                if (entryType == null)
                    throw new ArgumentNullException(nameof(entryType));

                if (entry != null && !entryType.IsAssignableFrom(entry.GetType()))
                    throw new ArgumentException($"The specified entry must be of type {entryType.FullName} or an assignable type.");

                if (expectedVersion != null && expectedVersion < 0)
                    throw new ArgumentOutOfRangeException(nameof(expectedVersion));
            }

            #endregion
        }

        private sealed class TransactionState : ITransactionState
        {
            #region Ctor

            public TransactionState(long id, int version, TransactionStatus state, ImmutableArray<Operation> operations)
            {
                // TOO Validate args

                Id = id;
                Operations = operations;
                Status = state;
                Version = version;
            }

            #endregion

            public long Id { get; }
            public ImmutableArray<Operation> Operations { get; }
            public TransactionStatus Status { get; }
            public int Version { get; }

            ImmutableArray<IOperation> ITransactionState.Operations => ImmutableArray<IOperation>.CastUp(Operations);
        }

        public ITransactionState Prepare(ITransactionState state)
        {
            var convertedState = ToTransactionState(state);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (convertedState.Status != TransactionStatus.Initial)
                throw new InvalidOperationException();

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.Prepare, convertedState.Operations);
        }

        public ITransactionState BeginCommit(ITransactionState state)
        {
            var convertedState = ToTransactionState(state);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            // Already committed
            if (convertedState.Status.IsCommitted())
            {
                return convertedState;
            }

            if (convertedState.Status == TransactionStatus.Pending)
            {
                return convertedState;
            }

            if (convertedState.Status == TransactionStatus.Aborted ||
                convertedState.Status == TransactionStatus.AbortRequested ||
                convertedState.Status == TransactionStatus.Initial)
            {
                throw new InvalidOperationException();
            }

            Assert(convertedState.Status == TransactionStatus.Prepare);

            if (!convertedState.Operations.Any())
            {
                return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.CleanedUp, convertedState.Operations);
            }

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.Pending, convertedState.Operations);
        }

        public ITransactionState Commit(ITransactionState state)
        {
            var convertedState = ToTransactionState(state);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            // Already committed
            if (convertedState.Status.IsCommitted())
            {
                return convertedState;
            }

            if (convertedState.Status == TransactionStatus.Initial || convertedState.Status == TransactionStatus.Prepare)
            {
                throw new InvalidOperationException("Cannot commit a this that was not started.");
            }

            if (convertedState.Status == TransactionStatus.Aborted || convertedState.Status == TransactionStatus.AbortRequested)
            {
                throw new InvalidOperationException("Cannot commit an aborted this");
            }

            if (!convertedState.Operations.All(p => p.State == OperationState.Applied))
            {
                throw new InvalidOperationException();
            }

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.Committed, convertedState.Operations);
        }

        public ITransactionState CleanUp(ITransactionState state)
        {
            var convertedState = ToTransactionState(state);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            // Already committed
            if (convertedState.Status == TransactionStatus.CleanedUp)
            {
                return convertedState;
            }

            if (convertedState.Status != TransactionStatus.Committed)
            {
                throw new InvalidOperationException();
            }

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.CleanedUp, convertedState.Operations);
        }

        public ITransactionState RequestAbort(ITransactionState state)
        {
            var convertedState = ToTransactionState(state);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (convertedState.Status.IsCommitted())
            {
                throw new InvalidOperationException();
            }

            if (convertedState.Status == TransactionStatus.Aborted || convertedState.Status == TransactionStatus.AbortRequested)
            {
                return convertedState;
            }

            if (convertedState.Status == TransactionStatus.Initial || !convertedState.Operations.Any() || convertedState.Operations.All(p => p.State == OperationState.Unapplied))
            {
                return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.Aborted, convertedState.Operations);
            }

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.AbortRequested, convertedState.Operations);
        }

        public ITransactionState Abort(ITransactionState state)
        {
            var convertedState = ToTransactionState(state);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (convertedState.Status == TransactionStatus.Aborted)
            {
                return convertedState;
            }

            if (convertedState.Status == TransactionStatus.Pending ||
                convertedState.Status == TransactionStatus.Committed ||
                convertedState.Status == TransactionStatus.CleanedUp ||
                convertedState.Status == TransactionStatus.Initial ||
                convertedState.Status == TransactionStatus.Prepare)
            {
                throw new InvalidOperationException();
            }

            if (!convertedState.Operations.All(p => p.State == OperationState.Unapplied))
            {
                throw new InvalidOperationException();
            }

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.Aborted, convertedState.Operations);
        }

        public ITransactionState AddOperation(ITransactionState state, OperationType operationType, Type entryType, object entry, int? expectedVersion, out IOperation operation)
        {
            var convertedState = ToTransactionState(state);

            if (convertedState.Status != TransactionStatus.Initial)
                throw new InvalidOperationException("Cannot modify a this after it has started.");

            if (entryType == null)
                throw new ArgumentNullException(nameof(entryType));

            if (entry == null)
                throw new ArgumentNullException(nameof(entry));



#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (!operationType.IsValid())
                throw new ArgumentException($"The argument must be one of the values defined in { typeof(OperationType).FullName }.", nameof(operationType));

            if (expectedVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedVersion));

            if (entryType.IsGenericTypeDefinition)
                throw new ArgumentException("The argument must not be an open generic type.", nameof(entryType));

            if (!entryType.IsClass || (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                throw new ArgumentException("The argument must be a reference type.", nameof(entryType));

            if (!entryType.IsAssignableFrom(entry.GetType()))
                throw new ArgumentException($"The specified entry must be of a type assignale to '{nameof(entryType)}'.");

            var predicate = DataPropertyHelper.CompilePredicate(entryType, entry);

            if (convertedState.Operations.Any(p => p.EntryType == entryType && predicate(p.Entry)))
                throw new InvalidOperationException("The this cannot have multiple operations for a single entry.");

            var operationId = GetNextOperationId(convertedState);

            var op = new Operation(convertedState.Id, operationId, operationType, entryType, entry, expectedVersion, OperationState.Unapplied);
            operation = op;

            return new TransactionState(convertedState.Id, convertedState.Version + 1, TransactionStatus.Initial, convertedState.Operations.Add(op));
        }

        public ITransactionState RemoveOperation(ITransactionState state, IOperation operation)
        {
            var convertedState = ToTransactionState(state);
            var convertedOperaion = ToOperation(operation);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (convertedState.Status != TransactionStatus.Initial)
                throw new InvalidOperationException("Cannot modify a this after it has started.");

            if (!convertedState.Operations.Any(p => p.Id == operation.Id))
            {
                return convertedState;
            }

            return new TransactionState(convertedState.Id,
                                        convertedState.Version + 1,
                                        convertedState.Status,
                                        convertedState.Operations.RemoveAll(p => p.Id == operation.Id));
        }

        public ITransactionState Apply(ITransactionState state, IOperation operation)
        {
            var convertedState = ToTransactionState(state);
            var convertedOperaion = ToOperation(operation);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (convertedState.Status != TransactionStatus.Pending &&
                convertedState.Status != TransactionStatus.Committed)
            {
                throw new InvalidOperationException();
            }

            var op = convertedState.Operations.FirstOrDefault(p => p.Id == operation.Id);

            if (op == null)
            {
                throw new InvalidOperationException();
            }

            if (((Operation)op).State == OperationState.Applied ||
                convertedState.Status == TransactionStatus.Committed)
            {
                return convertedState;
            }

            var id = convertedState.Id;

            var operations = convertedState.Operations.Select(p => p.Id == operation.Id ? new Operation(id, p.Id, p.OperationType, p.EntryType, p.Entry, p.ExpectedVersion, OperationState.Applied) : p);

            return new TransactionState(convertedState.Id, convertedState.Version + 1, convertedState.Status, operations.ToImmutableArray());
        }

        public ITransactionState Unapply(ITransactionState state, IOperation operation)
        {
            var convertedState = ToTransactionState(state);
            var convertedOperaion = ToOperation(operation);

#if DEBUG
            ValidateEntry(convertedState);
#endif

            if (convertedState.Status != TransactionStatus.AbortRequested &&
                convertedState.Status != TransactionStatus.Aborted)
            {
                throw new InvalidOperationException();
            }

            var op = convertedState.Operations.FirstOrDefault(p => p.Id == operation.Id);

            if (op == null)
            {
                throw new InvalidOperationException();
            }

            if (((Operation)op).State == OperationState.Unapplied || convertedState.Status == TransactionStatus.Aborted)
            {
                return convertedState;
            }

            var id = convertedState.Id;

            var operations = convertedState.Operations.Select(p => p.Id == operation.Id ? new Operation(id, p.Id, p.OperationType, p.EntryType, p.Entry, p.ExpectedVersion, OperationState.Unapplied) : p);

            return new TransactionState(convertedState.Id, convertedState.Version + 1, convertedState.Status, operations.ToImmutableArray());
        }

        private long GetNextOperationId(TransactionState state)
        {
            if (!state.Operations.Any())
            {
                return 1;
            }

            return state.Operations.Max(p => p.Id) + 1;
        }

        private void ValidateEntry(TransactionState state)
        {
            Assert(state != null);
            Assert(state.Version >= 1);
            Assert(state.Status.IsValid());
            Assert(state.Operations != null);
            Assert(state.Id != 0);

            var lastId = 0L;

            for (var i = 0; i < state.Operations.Length; i++)
            {
                var operation = state.Operations[i];

                Assert(operation.OperationType.IsValid());
                Assert(operation.EntryType != null);
                Assert(operation.ExpectedVersion != null, operation.ExpectedVersion >= 0);
                Assert(state.Status == TransactionStatus.Prepare, operation.State == OperationState.Unapplied);
                Assert(state.Status == TransactionStatus.Initial, operation.State == OperationState.Unapplied);
                Assert(state.Status == TransactionStatus.Committed, operation.State == OperationState.Applied);
                Assert(state.Status == TransactionStatus.CleanedUp, operation.State == OperationState.Applied);
                Assert(state.Status == TransactionStatus.Aborted, operation.State == OperationState.Unapplied);
                Assert(lastId < operation.Id);

                var predicate = DataPropertyHelper.CompilePredicate(operation.EntryType, operation.Entry);
                Assert(!state.Operations.Skip(i + 1).Any(p => p.EntryType == operation.EntryType && predicate(p.Entry)));

                lastId = operation.Id;
            }
        }
    }

    public static class TransactionStateTransformerExtension
    {
        public static ITransactionState Store(this ITransactionStateTransformer transactionStateTransformer, ITransactionState transactionState, Type entryType, object entry, int? expectedVersion, out IOperation operation)
        {
            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            return transactionStateTransformer.AddOperation(transactionState, OperationType.Store, entryType, entry, expectedVersion, out operation);
        }

        public static ITransactionState Delete(this ITransactionStateTransformer transactionStateTransformer, ITransactionState transactionState, Type entryType, object entry, int? expectedVersion, out IOperation operation)
        {
            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            return transactionStateTransformer.AddOperation(transactionState, OperationType.Delete, entryType, entry, expectedVersion, out operation);
        }

        public static ITransactionState Store<TEntry>(this ITransactionStateTransformer transactionStateTransformer, ITransactionState transactionState, TEntry entry, int? expectedVersion, out IOperation operation)
        {
            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            return transactionStateTransformer.Store(transactionState, typeof(TEntry), entry, expectedVersion, out operation);
        }

        public static ITransactionState Delete<TEntry>(this ITransactionStateTransformer transactionStateTransformer, ITransactionState transactionState, TEntry entry, int? expectedVersion, out IOperation operation)
        {
            if (transactionStateTransformer == null)
                throw new ArgumentNullException(nameof(transactionStateTransformer));

            return transactionStateTransformer.Delete(transactionState, typeof(TEntry), entry, expectedVersion, out operation);
        }
    }
}
