/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using static AI4E.Storage.MongoDB.MongoExceptionHelper;

namespace AI4E.Storage.MongoDB
{
    /// <summary>
    /// Represents a scope of a MongoDB database.
    /// </summary>
    public sealed class MongoDatabaseScope : IDatabaseScope
    {
        private volatile int _operationInProgress = 0;

        private readonly MongoDatabase _owner;
        private readonly ILogger<MongoDatabaseScope> _logger;
        private readonly DisposableAsyncLazy<IClientSessionHandle> _clientSessionHandle;

        private bool _abortTransaction = false;

        /// <summary>
        /// Creates a new instance of the <see cref="MongoDatabaseScope"/> type.
        /// </summary>
        /// <param name="database">The <see cref="MongoDatabase"/> that is the scope owner.</param>
        /// <param name="logger">
        /// A <see cref="ILogger{MongoDatabaseScope}"/> used for logging or <c>null</c> to disable logging.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="database"/> is <c>null</c>.</exception>
        public MongoDatabaseScope(MongoDatabase database, ILogger<MongoDatabaseScope>? logger = null)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            _owner = database;
            _logger = logger ?? NullLogger<MongoDatabaseScope>.Instance;
            _clientSessionHandle = new DisposableAsyncLazy<IClientSessionHandle>(
                CreateClientSessionHandleAsync,
                DisposeClientSessionHandleAsync,
                DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        #region ClientSessionHandle

        private Task<IClientSessionHandle> GetClientSessionHandleAsync(CancellationToken cancellation)
        {
            return _clientSessionHandle.Task.WithCancellation(cancellation);
        }

        private async Task<IClientSessionHandle> CreateClientSessionHandleAsync(CancellationToken cancellation)
        {
            var mongoClient = _owner.UnderlyingDatabase.Client;
            var clientSessionHandle = await mongoClient.StartSessionAsync(cancellationToken: cancellation)
                .ConfigureAwait(false);

            _logger.LogTrace(
                "Created mongo client session handle with id " + clientSessionHandle.WrappedCoreSession.Id.ToString());

            return clientSessionHandle;
        }

        private async Task DisposeClientSessionHandleAsync(IClientSessionHandle clientSessionHandle)
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

        /// <inheritdoc/>
        public async ValueTask<bool> StoreAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry?, bool>> predicate,
            CancellationToken cancellation) where TEntry : class
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

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
                    Debug.Assert(!(await GetClientSessionHandleAsync(cancellation).ConfigureAwait(false)).IsInTransaction);
#endif
                    return false;
                }

                var session = await EnsureTransactionAsync(cancellation).ConfigureAwait(false);
                var collection = await GetCollectionAsync<TEntry>(session, cancellation).ConfigureAwait(false);

                if (collection is null)
                {
                    Debug.Assert(_abortTransaction);
                    Debug.Assert(!session.IsInTransaction);
                    return false;
                }

                var idPredicate = DataPropertyHelper.BuildPredicate(entry);

                // When we have no identity, always perform an insertion.
                if (idPredicate is null)
                {
                    var compiledPredicate = predicate.Compile(preferInterpretation: true);

                    if (!compiledPredicate(null))
                    {
                        return false;
                    }

                    idPredicate = _ => false;
                }
                else if (!await ValidatePredicateAsync(collection, session, idPredicate, predicate, cancellation)
                    .ConfigureAwait(false))
                {
                    return false;
                }

                _logger.LogTrace(
                    $"Storing an entry of type '{typeof(TEntry)}' via " +
                    $"mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                ReplaceOneResult updateResult;

                try
                {
                    updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(
                        session,
                        idPredicate,
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
                    return false;
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

                return true;
            }
            finally
            {
                _operationInProgress = 0; // Volatile write op
            }
        }

        private async ValueTask<bool> ValidatePredicateAsync<TEntry>(
            IMongoCollection<TEntry> collection,
            IClientSessionHandle session,
            Expression<Func<TEntry, bool>> idPredicate,
            Expression<Func<TEntry?, bool>> predicate,
            CancellationToken cancellation) where TEntry : class
        {
            // We often use a construct _ => true or _ => false as predicate. 
            // Test these special cases to get a performance gain for this.
            if (predicate.Body.TryEvaluate(out var evaluationResult))
            {
                Debug.Assert(evaluationResult is bool);

                return (bool)evaluationResult;
            }

            using var cursor = await collection.FindAsync(
                session,
                idPredicate,
                new FindOptions<TEntry, TEntry> { Limit = 1 },
                cancellation).ConfigureAwait(false);

            var entry = await cursor.FirstOrDefaultAsync(cancellation).ConfigureAwait(false);
            var compiledPredicate = predicate.Compile(preferInterpretation: true);

            return compiledPredicate(entry);
        }

        /// <inheritdoc/>
        public async ValueTask<bool> RemoveAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry?, bool>> predicate,
            CancellationToken cancellation) where TEntry : class
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

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
                    var clientSessionHandler = await GetClientSessionHandleAsync(cancellation).ConfigureAwait(false);
                    Debug.Assert(!clientSessionHandler.IsInTransaction);
#endif
                    return false;
                }

                var session = await EnsureTransactionAsync(cancellation).ConfigureAwait(false);
                var idPredicate = DataPropertyHelper.BuildPredicate(entry);
                var collection = await GetCollectionAsync<TEntry>(session, cancellation).ConfigureAwait(false);

                if (idPredicate is null)
                {
                    // Cannot remove an entry that has no identity.
                    return false;
                }

                if (collection is null)
                {
                    Debug.Assert(_abortTransaction);
                    Debug.Assert(!session.IsInTransaction);
                    return false;
                }

                if (!await ValidatePredicateAsync(collection, session, idPredicate, predicate, cancellation)
                    .ConfigureAwait(false))
                {
                    return false;
                }

                _logger.LogTrace(
                    $"Removing an entry of type '{typeof(TEntry)}' via " +
                    $"mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                DeleteResult deleteResult;

                try
                {
                    deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(
                        session,
                        idPredicate,
                        cancellationToken: cancellation)).ConfigureAwait(false);
                }
                catch (MongoCommandException exc) when
                    (exc.Code == (int)MongoErrorCode.WriteConflict
                    || exc.Code == (int)MongoErrorCode.LockTimeout
                    || exc.Code == (int)MongoErrorCode.NoSuchTransaction)
                {
                    await AbortAsync(session, cancellation).ConfigureAwait(false);
                    return false;
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

                return deleteResult.DeletedCount > 0;
            }
            finally
            {
                _operationInProgress = 0; // Volatile write op
            }
        }

        /// <inheritdoc/>
        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation) where TEntry : class
        {
            async ValueTask<IAsyncCursor<TEntry>?> BuildAsyncCursor(CancellationToken sequenceCancellation)
            {
                var session = await GetClientSessionHandleAsync(sequenceCancellation).ConfigureAwait(false);

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
                    var collection = await GetCollectionAsync<TEntry>(session, sequenceCancellation)
                        .ConfigureAwait(false);

                    if (collection == null)
                    {
                        Debug.Assert(_abortTransaction);
                        Debug.Assert(!session.IsInTransaction);
                        return null;
                    }

                    _logger.LogTrace(
                        $"Performing query " +
                        $"via mongo client session handle '{ session.WrappedCoreSession.Id}'.");

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

        /// <inheritdoc/>
        public async IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
           Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
           [EnumeratorCancellation]  CancellationToken cancellation) where TEntry : class
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

        private async Task<IMongoCollection<TEntry>?> GetCollectionAsync<TEntry>(
            IClientSessionHandle session,
            CancellationToken cancellation) where TEntry : class
        {
            var collection = await _owner.GetCollectionAsync<TEntry>(isInTransaction: true, cancellation)
                .ConfigureAwait(false);

            if (collection is null)
            {
                _logger.LogTrace(
                    $"Trying to create a collection inside " +
                    $"transaction of mongo client session handle '{ session.WrappedCoreSession.Id}'.");

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
            var session = await GetClientSessionHandleAsync(cancellation)
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

        /// <inheritdoc/>
        public async ValueTask<bool> TryCommitAsync(CancellationToken cancellation)
        {
            var session = await GetClientSessionHandleAsync(cancellation)
                .ConfigureAwait(false);

            _logger.LogTrace($"Committing transaction of mongo client " +
                $"session handle '{ session.WrappedCoreSession.Id}'.");

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

        /// <inheritdoc/>
        public async ValueTask RollbackAsync(CancellationToken cancellation)
        {
            var session = await GetClientSessionHandleAsync(cancellation)
                .ConfigureAwait(false);

            _logger.LogTrace(
                $"Aborting transaction of mongo client " +
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

        /// <inheritdoc/>
        public void Dispose()
        {
            _clientSessionHandle.Dispose();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            return _clientSessionHandle.DisposeAsync();
        }
    }
}
