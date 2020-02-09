using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Diagnostics;
using static AI4E.Storage.MongoDB.MongoExceptionHelper;
using System.Runtime.CompilerServices;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoDatabaseScope : IDatabaseScope
    {
        private volatile int _operationInProgress = 0;

        private readonly MongoDatabase _owner;
        private readonly ILogger<MongoDatabaseScope> _logger;
        private readonly DisposableAsyncLazy<IClientSessionHandle> _clientSessionHandle;

        private bool _abortTransaction = false;

        public MongoDatabaseScope(MongoDatabase owner, ILogger<MongoDatabaseScope> logger)
        {
            _owner = owner;
            _logger = logger;
            _clientSessionHandle = new DisposableAsyncLazy<IClientSessionHandle>(
                CreateClientSessionHandle,
                DisposeClientSessionHandle,
                DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        #region ClientSessionHandle

        private Task<IClientSessionHandle> GetClientSessionHandle(CancellationToken cancellation)
        {
            return _clientSessionHandle.Task.WithCancellation(cancellation);
        }

        private async Task<IClientSessionHandle> CreateClientSessionHandle(CancellationToken cancellation)
        {
            var mongoClient = _owner.UnderlyingDatabase.Client;
            var clientSessionHandle = await mongoClient.StartSessionAsync(cancellationToken: cancellation)
                .ConfigureAwait(false);

            _logger.LogTrace(
                "Created mongo client session handle with id " + clientSessionHandle.WrappedCoreSession.Id.ToString());

            return clientSessionHandle;
        }

        private async Task DisposeClientSessionHandle(IClientSessionHandle clientSessionHandle)
        {
            var id = clientSessionHandle.WrappedCoreSession.Id;
            Debug.Assert(clientSessionHandle != null);
            try
            {
                if (clientSessionHandle!.IsInTransaction)
                {
                    await clientSessionHandle.AbortTransactionAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                clientSessionHandle!.Dispose();
            }

            _logger.LogTrace("Released mongo client session handle with id " + id.ToString());
        }

        #endregion

        public async ValueTask StoreAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (Interlocked.Exchange(ref _operationInProgress, 1) != 0)
            {
                throw new InvalidOperationException(
                    "MongoDB does not allow interlaced operations on a single transaction.");
            }

            try
            {
                if (_abortTransaction)
                {
#if DEBUG
                    Debug.Assert(!(await GetClientSessionHandle(cancellation).ConfigureAwait(false)).IsInTransaction);
#endif
                    return;
                }

                var session = await EnsureTransactionAsync(cancellation).ConfigureAwait(false);
                var predicate = DataPropertyHelper.BuildPredicate(entry);
                var collection = await GetCollection<TEntry>(session, cancellation).ConfigureAwait(false);

                if (collection == null)
                {
                    Debug.Assert(_abortTransaction);
                    Debug.Assert(!session.IsInTransaction);
                    return;
                }

                _logger.LogTrace(
                    $"Storing an entry of type '{typeof(TEntry)}' via " +
                    $"mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                ReplaceOneResult updateResult;

                try
                {
                    updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(
                        session,
                        predicate,
                        entry,
                        options: new UpdateOptions { IsUpsert = true },
                        cancellationToken: cancellation)).ConfigureAwait(false);
                }
                catch (MongoCommandException exc) when
                    (exc.Code == (int)MongoErrorCode.WriteConflict
                    || exc.Code == (int)MongoErrorCode.LockTimeout
                    || exc.Code == (int)MongoErrorCode.NoSuchTransaction)
                {
                    await AbortAsync(session, cancellation).ConfigureAwait(false);
                    return;
                }
                catch
                {
                    await AbortAsync(session, cancellation).ConfigureAwait(false);
                    throw;
                }

                if (!updateResult.IsAcknowledged)
                {
                    throw new StorageException();
                }

                Debug.Assert(updateResult.MatchedCount == 0 || updateResult.MatchedCount == 1);
            }
            finally
            {
                _operationInProgress = 0; // Volatile write op
            }
        }

        public async ValueTask RemoveAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (Interlocked.Exchange(ref _operationInProgress, 1) != 0)
            {
                throw new InvalidOperationException(
                    "MongoDB does not allow interlaced operations on a single transaction.");
            }
            try
            {
                if (_abortTransaction)
                {
#if DEBUG
                    Debug.Assert(!(await GetClientSessionHandle(cancellation).ConfigureAwait(false)).IsInTransaction);
#endif
                    return;
                }

                var session = await EnsureTransactionAsync(cancellation).ConfigureAwait(false);
                var predicate = DataPropertyHelper.BuildPredicate(entry);
                var collection = await GetCollection<TEntry>(session, cancellation).ConfigureAwait(false);

                if (collection == null)
                {
                    Debug.Assert(_abortTransaction);
                    Debug.Assert(!session.IsInTransaction);
                    return;
                }

                _logger.LogTrace(
                    $"Removing an entry of type '{typeof(TEntry)}' via " +
                    $"mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                DeleteResult deleteResult;

                try
                {
                    deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(
                        session,
                        predicate,
                        cancellationToken: cancellation)).ConfigureAwait(false);
                }
                catch (MongoCommandException exc) when
                    (exc.Code == (int)MongoErrorCode.WriteConflict
                    || exc.Code == (int)MongoErrorCode.LockTimeout
                    || exc.Code == (int)MongoErrorCode.NoSuchTransaction)
                {
                    await AbortAsync(session, cancellation).ConfigureAwait(false);
                    return;
                }
                catch
                {
                    await AbortAsync(session, cancellation).ConfigureAwait(false);
                    throw;
                }

                if (!deleteResult.IsAcknowledged)
                {
                    throw new StorageException();
                }

                Debug.Assert(deleteResult.DeletedCount == 0 || deleteResult.DeletedCount == 1);
            }
            finally
            {
                _operationInProgress = 0; // Volatile write op
            }
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            async ValueTask<IAsyncCursor<TEntry>?> BuildAsyncCursor(CancellationToken sequenceCancellation)
            {
                var session = await GetClientSessionHandle(sequenceCancellation).ConfigureAwait(false);

                if (Interlocked.Exchange(ref _operationInProgress, 1) != 0)
                {
                    throw new InvalidOperationException(
                        "MongoDB does not allow interlaced operations on a single transaction.");
                }

                try
                {
                    if (_abortTransaction)
                    {
                        Debug.Assert(!session.IsInTransaction);
                        return null;
                    }

                    EnsureTransaction(session);
                    var collection = await GetCollection<TEntry>(session, sequenceCancellation)
                        .ConfigureAwait(false);

                    if (collection == null)
                    {
                        Debug.Assert(_abortTransaction);
                        Debug.Assert(!session.IsInTransaction);
                        return null;
                    }

                    _logger.LogTrace(
                        $"Performing query " +
                        $"via mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                    return await collection.FindAsync<TEntry>(
                        session,
                        predicate,
                        cancellationToken: sequenceCancellation).ConfigureAwait(false);
                }
                catch (MongoCommandException exc) when
                    (exc.Code == (int)MongoErrorCode.LockTimeout
                    || exc.Code == (int)MongoErrorCode.NoSuchTransaction)
                {
                    await AbortAsync(session, sequenceCancellation).ConfigureAwait(false);
                    return null;
                }
                catch
                {
                    await AbortAsync(session, sequenceCancellation).ConfigureAwait(false);
                    throw;
                }
            }

            void AsyncCursorDisposal(IAsyncCursor<TEntry>? asyncCursor)
            {
                asyncCursor?.Dispose();

                _operationInProgress = 0; // Volatile write op
            }

            return new MongoQueryEvaluator<TEntry>(BuildAsyncCursor, AsyncCursorDisposal);
        }

        public async IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
           Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
           [EnumeratorCancellation]  CancellationToken cancellation = default)
           where TEntry : class
        {
            if (queryShaper is null)
                throw new ArgumentNullException(nameof(queryShaper));

            // TODO: We need 
            //       https://jira.mongodb.org/browse/CSHARP-2596
            //       https://github.com/mongodb/mongo-csharp-driver/pull/371
            //       to implement this properly.
            //       Is there a workaround other then an all in-memory processing?

            var entries = await GetAsync<TEntry>(_ => true, cancellation);
            var queryable = entries.AsQueryable();
            foreach (var entry in queryShaper(queryable))
            {
                yield return entry;
            }
        }

        private async Task<IMongoCollection<TEntry>?> GetCollection<TEntry>(
            IClientSessionHandle session,
            CancellationToken cancellation)
            where TEntry : class
        {
            var collection = await _owner.GetCollectionAsync<TEntry>(isInTransaction: true, cancellation)
                .ConfigureAwait(false);

            if (collection is null)
            {
                _logger.LogTrace(
                    $"Trying to create a collection inside " +
                    $"txn of mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                await AbortAsync(session, cancellation)
                    .ConfigureAwait(false);

                // We are not in a transaction any more any can freely create the collection and
                // await its creation in order that the collection is already there if the transaction is re-executed.
                await _owner.GetCollectionAsync<TEntry>(isInTransaction: false, cancellation)
                    .ConfigureAwait(false);
            }

            return collection;
        }

        private async Task<IClientSessionHandle> EnsureTransactionAsync(CancellationToken cancellation)
        {
            var session = await GetClientSessionHandle(cancellation)
                .ConfigureAwait(false);

            EnsureTransaction(session);

            return session;
        }

        private void EnsureTransaction(IClientSessionHandle session)
        {
            Debug.Assert(!_abortTransaction);

            if (!session.IsInTransaction)
            {
                session.StartTransaction();
            }
        }

        public async ValueTask<bool> TryCommitAsync(CancellationToken cancellation = default)
        {
            var session = await GetClientSessionHandle(cancellation)
                .ConfigureAwait(false);

            _logger.LogTrace($"Committing txn of mongo client " +
                $"session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

            _operationInProgress = 0; // Volatile write op

            if (_abortTransaction)
            {
                // Reset this to allocate a new transaction when calling any read/write operation.
                _abortTransaction = false;
                Debug.Assert(!session.IsInTransaction);
                return false;
            }

            if (!session.IsInTransaction)
            {
                return true;
            }

            try
            {
                await session.CommitTransactionAsync(cancellation)
                    .ConfigureAwait(false);
                return true;
            }
            catch (MongoCommandException exc) when (exc.Code == (int)MongoErrorCode.NoSuchTransaction)
            {
                return false;
            }
            catch (Exception) // TODO: Specify exception type
            {
                //await session.AbortTransactionAsync();
                return false;
            }
        }

        public async ValueTask RollbackAsync(CancellationToken cancellation)
        {
            var session = await GetClientSessionHandle(cancellation)
                .ConfigureAwait(false);

            _logger.LogTrace(
                $"Aborting txn of mongo client " +
                $"session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

            await AbortAsync(session, cancellation).ConfigureAwait(false);

            // Reset this to allocate a new transaction when calling any read/write operation.
            _abortTransaction = false;
        }

        private async Task AbortAsync(IClientSessionHandle session, CancellationToken cancellation)
        {
            _abortTransaction = true;
            _operationInProgress = 0; // Volatile write op

            if (!session.IsInTransaction)
            {
                return;
            }

            await session.AbortTransactionAsync(cancellation).ConfigureAwait(false);

            Debug.Assert(!session.IsInTransaction);
        }

        public void Dispose()
        {
            _clientSessionHandle.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _clientSessionHandle.DisposeAsync();
        }
    }
}
