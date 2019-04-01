/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MongoDatabase.cs 
 * Types:           (1) AI4E.Storage.MongoDB.MongoDatabase
 * Version:         1.0
 * Author:          Andreas Trütschel
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.MongoDB.Serializers;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;
using static AI4E.Storage.MongoDB.MongoWriteHelper;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoDatabase : IQueryableDatabase
    {
        private const string _collectionLookupEntryResourceKey = "collection-lookup-entry";
        private const string _counterEntryCollectionName = "data-store-counter";
        private const string _collectionLookupCollectionName = "data-store-collection-lookup";

        #region Fields

        private readonly IMongoDatabase _database;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<MongoDatabase> _logger;

        private volatile ImmutableDictionary<Type, Task> _collections = ImmutableDictionary<Type, Task>.Empty;
        private readonly object _collectionsLock = new object();

        private readonly IMongoCollection<CounterEntry> _counterCollection;
        private readonly IMongoCollection<CollectionLookupEntry> _collectionLookupCollection;

        #endregion

        #region C'tor

        static MongoDatabase()
        {
            BsonSerializer.RegisterSerializationProvider(new StructSerializationProvider());
            BsonSerializer.RegisterSerializationProvider(new DictionarySerializerProvider());
            var conventionPack = new ConventionPack
            {
                new ClassMapConvention()
            };
            ConventionRegistry.Register("AI4E default convention pack", conventionPack, _ => true);
        }

        private sealed class ClassMapConvention : IClassMapConvention
        {
            public string Name => typeof(ClassMapConvention).ToString();

            public void Apply(BsonClassMap classMap)
            {
                var idMember = DataPropertyHelper.GetIdMember(classMap.ClassType);

                if (idMember != null)
                {
                    classMap.MapIdMember(idMember);
                }
            }
        }

        public MongoDatabase(IMongoDatabase database, ILoggerFactory loggerFactory = null)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<MongoDatabase>();
            _counterCollection = _database.GetCollection<CounterEntry>(_counterEntryCollectionName);
            _collectionLookupCollection = _database.GetCollection<CollectionLookupEntry>(_collectionLookupCollectionName);
        }

        #endregion

        #region Resource id

        public async ValueTask<long> GetUniqueResourceIdAsync(string resourceKey, CancellationToken cancellation)
        {
            if (resourceKey == null)
                throw new ArgumentNullException(nameof(resourceKey));

            var counterEntry = await _counterCollection.FindOneAndUpdateAsync<CounterEntry, CounterEntry>(
                                p => p.ResourceKey == resourceKey,
                                Builders<CounterEntry>.Update.Inc(p => p.Counter, 1),
                                new FindOneAndUpdateOptions<CounterEntry, CounterEntry>
                                {
                                    ReturnDocument = ReturnDocument.After,
                                    IsUpsert = true
                                }, cancellation);

            return counterEntry.Counter;
        }

        private sealed class CounterEntry
        {
            [BsonId]
            public string ResourceKey { get; set; }

            public int Counter { get; set; }
        }

        #endregion

        #region Collection lookup

        private Task<IMongoCollection<TEntry>> GetCollectionAsync<TEntry>(CancellationToken cancellation)
        {
            return GetCollectionAsync<TEntry>(isInTransaction: false, cancellation);
        }

        private Task<IMongoCollection<TEntry>> GetCollectionAsync<TEntry>(bool isInTransaction, CancellationToken cancellation)
        {
            Task<IMongoCollection<TEntry>> result;

            if (_collections.TryGetValue(typeof(TEntry), out var entry))
            {
                result = (Task<IMongoCollection<TEntry>>)entry;
            }
            else
            {
                lock (_collectionsLock)
                {
                    if (_collections.TryGetValue(typeof(TEntry), out entry))
                    {
                        result = (Task<IMongoCollection<TEntry>>)entry;
                    }
                    else
                    {
                        var lazy = new AsyncLazy<IMongoCollection<TEntry>>(
                                            CreateCollectionAsync<TEntry>,
                                            AsyncLazyFlags.ExecuteOnCallingThread | AsyncLazyFlags.RetryOnFailure);

                        result = lazy.Task;
                        _collections = _collections.Add(typeof(TEntry), result);
                    }
                }
            }

            // Cannot create a collection while beeing in a transaction.
            if (isInTransaction && !result.IsCompleted)
            {
                return Task.FromResult<IMongoCollection<TEntry>>(null);
            }

            return result;
        }

        private async Task<IMongoCollection<TEntry>> CreateCollectionAsync<TEntry>()
        {
            var collectionKey = GetCollectionKey<TEntry>();
            var collectionName = await GetCollectionNameAsync(collectionKey);

            var collection = await GetCollectionCoreAsync<TEntry>(collectionName);
            return collection;
        }

        private static string GetCollectionKey<TEntry>()
        {
            return typeof(TEntry).ToString();
        }

        private async Task<string> GetCollectionNameAsync(string collectionKey)
        {
            var lookupEntry = await _collectionLookupCollection.AsQueryable()
                                                               .FirstOrDefaultAsync(p => p.CollectionKey == collectionKey);

            if (lookupEntry != null)
            {
                return lookupEntry.CollectionName;
            }

            // There is no collection entry. We have to allocate one. Get a unique key.
            var collectionId = await GetUniqueResourceIdAsync(_collectionLookupEntryResourceKey, cancellation: default); // TODO: Use the disposal token

            try
            {
                lookupEntry = new CollectionLookupEntry { CollectionKey = collectionKey, CollectionName = "data-store#" + collectionId };
                await TryWriteOperation(() => _collectionLookupCollection.InsertOneAsync(lookupEntry, default, cancellationToken: default));// TODO: Use the disposal token
            }
            catch (DuplicateKeyException)
            {
                lookupEntry = await _collectionLookupCollection.AsQueryable()
                                                               .FirstOrDefaultAsync(p => p.CollectionKey == collectionKey);
            }

            Assert(lookupEntry != null);

            return lookupEntry.CollectionName;
        }

        private async Task<bool> CollectionExistsAsync(string collectionName)
        {
            var collections = await _database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", collectionName) });

            return await collections.AnyAsync();
        }

        private async Task<IMongoCollection<TEntry>> GetCollectionCoreAsync<TEntry>(string collectionName)
        {
            while (!await CollectionExistsAsync(collectionName))
            {
                try
                {
                    _database.CreateCollection(collectionName);
                }
                catch (MongoCommandException exc) { } // TODO: Check error code
            }

            var result = _database.GetCollection<TEntry>(collectionName);
            return result;
        }

        private sealed class CollectionLookupEntry
        {
            [BsonId]
            public string CollectionKey { get; set; }

            public string CollectionName { get; set; }
        }

        #endregion

        #region BuildPredicate

        private static Expression<Func<TEntry, bool>> BuildPredicate<TEntry>(TEntry comparand,
                                                                             Expression<Func<TEntry, bool>> predicate)
        {
            Assert(comparand != null);
            Assert(predicate != null);

            var parameter = predicate.Parameters.First();
            var idSelector = DataPropertyHelper.BuildPredicate(comparand);

            var body = Expression.AndAlso(ParameterExpressionReplacer.ReplaceParameter(idSelector.Body, idSelector.Parameters.First(), parameter),
                                          predicate.Body);

            return Expression.Lambda<Func<TEntry, bool>>(body, parameter);
        }

        #endregion

        #region IDatabase

        public async Task<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation = default) where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var collection = await GetCollectionAsync<TEntry>(cancellation);
            
            try
            {
                await TryWriteOperation(() => collection.InsertOneAsync(entry, new InsertOneOptions { }, cancellation));
            }
            catch (DuplicateKeyException)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> RemoveAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var collection = await GetCollectionAsync<TEntry>(cancellation);
            var deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(BuildPredicate(entry, predicate), cancellationToken: cancellation));

            if (!deleteResult.IsAcknowledged)
            {
                throw new StorageException();
            }

            Assert(deleteResult.DeletedCount == 0 || deleteResult.DeletedCount == 1);

            return deleteResult.DeletedCount > 0;
        }

        public async Task Clear<TEntry>(CancellationToken cancellation = default)
             where TEntry : class
        {
            var collection = await GetCollectionAsync<TEntry>(cancellation);
            var deleteManyResult = await TryWriteOperation(() => collection.DeleteManyAsync(p => true, cancellation));

            if (!deleteManyResult.IsAcknowledged)
            {
                throw new StorageException();
            }
        }

        public async Task<bool> UpdateAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var collection = await GetCollectionAsync<TEntry>(cancellation);
            var updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(BuildPredicate(entry, predicate),
                                                                                        entry,
                                                                                        options: new UpdateOptions { IsUpsert = false },
                                                                                        cancellationToken: cancellation));

            if (!updateResult.IsAcknowledged)
            {
                throw new StorageException();
            }

            Assert(updateResult.MatchedCount == 0 || updateResult.MatchedCount == 1);

            return updateResult.MatchedCount != 0; // TODO: What does the result value represent? Matched count or modified count.
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(Expression<Func<TEntry, bool>> predicate,
                                                         CancellationToken cancellation = default)
           where TEntry : class
        {
            var collection = GetCollectionAsync<TEntry>(cancellation);
            return new MongoQueryEvaluator<TEntry>(collection, predicate, cancellation);
        }

        public async ValueTask<TEntry> GetOneAsync<TEntry>(Expression<Func<TEntry, bool>> predicate,
                                                           CancellationToken cancellation = default)
            where TEntry : class
        {
            var collection = await GetCollectionAsync<TEntry>(cancellation);

            using (var cursor = await collection.FindAsync(predicate, new FindOptions<TEntry, TEntry> { Limit = 1 }, cancellation))
            {
                return await cursor.FirstOrDefaultAsync(cancellation);
            }
        }

        bool IDatabase.SupportsScopes => true; // TODO: Check whether the underlying mongo database actually supports transactions.

        public IScopedDatabase CreateScope()
        {
            return new MongoScopedTransactionalDatabase(this);
        }

        #endregion

        #region IQueryableDatabase

        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
                                                                     CancellationToken cancellation = default)
            where TEntry : class
        {
            async ValueTask<IAsyncCursorSource<TResult>> GetCursorSourceAsync()
            {
                var collection = await GetCollectionAsync<TEntry>(cancellation);

                return (IMongoQueryable<TResult>)queryShaper(collection.AsQueryable());
            }

            var queryEvaluator = new MongoQueryEvaluator<TResult>(GetCursorSourceAsync(), cancellation);

            return queryEvaluator;
        }

        #endregion

        private sealed class MongoScopedTransactionalDatabase : IScopedDatabase
        {
            private volatile int _operationInProgress = 0;

            private readonly MongoDatabase _owner;
            private readonly ILogger<MongoScopedTransactionalDatabase> _logger;
            private readonly DisposableAsyncLazy<IClientSessionHandle> _clientSessionHandle;

            private bool _abortTransaction = false;

            public MongoScopedTransactionalDatabase(MongoDatabase owner)
            {
                _owner = owner;
                _logger = owner._loggerFactory?.CreateLogger<MongoScopedTransactionalDatabase>();
                _clientSessionHandle = new DisposableAsyncLazy<IClientSessionHandle>(CreateClientSessionHandle, DisposeClientSessionHandle, DisposableAsyncLazyOptions.ExecuteOnCallingThread);
            }

            #region ClientSessionHandle

            private Task<IClientSessionHandle> GetClientSessionHandle(CancellationToken cancellation)
            {
                return _clientSessionHandle.Task.WithCancellation(cancellation);
            }

            private async Task<IClientSessionHandle> CreateClientSessionHandle(CancellationToken cancellation)
            {
                var mongoClient = _owner._database.Client;
                var clientSessionHandle = await mongoClient.StartSessionAsync(cancellationToken: cancellation);

                _logger.LogTrace("Created mongo client session handle with id " + clientSessionHandle.WrappedCoreSession.Id.ToString());

                return clientSessionHandle;
            }

            private async Task DisposeClientSessionHandle(IClientSessionHandle clientSessionHandle)
            {
                var id = clientSessionHandle.WrappedCoreSession.Id;
                Assert(clientSessionHandle != null);
                try
                {
                    if (clientSessionHandle.IsInTransaction)
                    {
                        await clientSessionHandle.AbortTransactionAsync();
                    }
                }
                finally
                {
                    clientSessionHandle.Dispose();
                }

                _logger.LogTrace("Released mongo client session handle with id " + id.ToString());
            }

            #endregion

            #region IScopedTransactionalDatabase

            public async Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
                where TData : class
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (Interlocked.Exchange(ref _operationInProgress, 1) != 0)
                {
                    throw new InvalidOperationException("MongoDB does not allow interlaced operations on a single transaction.");
                }

                try
                {
                    if (_abortTransaction)
                    {
#if DEBUG
                        Assert(!(await GetClientSessionHandle(cancellation)).IsInTransaction);
#endif
                        return;
                    }

                    var session = await EnsureTransactionAsync(cancellation);
                    var predicate = DataPropertyHelper.BuildPredicate(data);
                    var collection = await GetCollection<TData>(session, cancellation);

                    if (collection == null)
                    {
                        Assert(_abortTransaction);
                        Assert(!session.IsInTransaction);
                        return;
                    }

                    _logger.LogTrace($"Storing an entry of type '{typeof(TData)}' via  mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                    ReplaceOneResult updateResult;

                    try
                    {
                        updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(session, predicate, data, options: new UpdateOptions { IsUpsert = true }, cancellationToken: cancellation));
                    }
                    catch (MongoCommandException exc) when (exc.Code == 112) // Write conflict.
                    {
                        await AbortAsync(session, cancellation);
                        return;
                    }

                    if (!updateResult.IsAcknowledged)
                    {
                        throw new StorageException();
                    }

                    Assert(updateResult.MatchedCount == 0 || updateResult.MatchedCount == 1);
                }
                finally
                {
                    _operationInProgress = 0; // Volatile write op
                }
            }

            public async Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
                where TData : class
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (Interlocked.Exchange(ref _operationInProgress, 1) != 0)
                {
                    throw new InvalidOperationException("MongoDB does not allow interlaced operations on a single transaction.");
                }
                try
                {
                    if (_abortTransaction)
                    {
#if DEBUG
                        Assert(!(await GetClientSessionHandle(cancellation)).IsInTransaction);
#endif
                        return;
                    }

                    var session = await EnsureTransactionAsync(cancellation);
                    var predicate = DataPropertyHelper.BuildPredicate(data);
                    var collection = await GetCollection<TData>(session, cancellation);

                    if (collection == null)
                    {
                        Assert(_abortTransaction);
                        Assert(!session.IsInTransaction);
                        return;
                    }

                    _logger.LogTrace($"Removing an entry of type '{typeof(TData)}' via  mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                    DeleteResult deleteResult;

                    try
                    {
                        deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(session, predicate, cancellationToken: cancellation));
                    }
                    catch (MongoCommandException exc) when (exc.Code == 112) // Write conflict.
                    {
                        await AbortAsync(session, cancellation);
                        return;
                    }

                    if (!deleteResult.IsAcknowledged)
                    {
                        throw new StorageException();
                    }

                    Assert(deleteResult.DeletedCount == 0 || deleteResult.DeletedCount == 1);
                }
                finally
                {
                    _operationInProgress = 0; // Volatile write op
                }
            }

            public IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
                where TData : class
            {
                async Task<IAsyncCursor<TData>> BuildAsyncCursor()
                {
                    var session = await GetClientSessionHandle(cancellation);

                    if (Interlocked.Exchange(ref _operationInProgress, 1) != 0)
                    {
                        throw new InvalidOperationException("MongoDB does not allow interlaced operations on a single transaction.");
                    }

                    try
                    {
                        if (_abortTransaction)
                        {
                            Assert(!session.IsInTransaction);
                            return null;
                        }

                        EnsureTransaction(session);
                        var collection = await GetCollection<TData>(session, cancellation);
                        if (collection == null)
                        {
                            Assert(_abortTransaction);
                            Assert(!session.IsInTransaction);
                            return null;
                        }

                        _logger.LogTrace($"Performing query via mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                        return await collection.FindAsync<TData>(session, predicate, cancellationToken: cancellation);
                    }
                    catch (MongoCommandException exc) when (exc.Code == 251) // No such transaction
                    {
                        await AbortAsync(session, cancellation);
                        return null;
                    }
                    catch
                    {
                        throw;
                    }
                }

                void AsyncCursorDisposal(IAsyncCursor<TData> asyncCursor)
                {
                    asyncCursor?.Dispose();

                    _operationInProgress = 0; // Volatile write op
                }

                return new MongoQueryEvaluator<TData>(BuildAsyncCursor, AsyncCursorDisposal);
            }

            private async Task<IMongoCollection<TData>> GetCollection<TData>(IClientSessionHandle session, CancellationToken cancellation)
                where TData : class
            {
                var collection = await _owner.GetCollectionAsync<TData>(isInTransaction: true, cancellation);

                if (collection == null)
                {
                    _logger.LogTrace($"Trying to create a collection inside txn of mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                    await AbortAsync(session, cancellation);

                    // We are not in a transaction any more any can freely create the collection and
                    // await its creation in order that the collection is already there if the transaction is re-executed.
                    await _owner.GetCollectionAsync<TData>(isInTransaction: false, cancellation);
                }

                return collection;
            }

            private async Task<IClientSessionHandle> EnsureTransactionAsync(CancellationToken cancellation)
            {
                var session = await GetClientSessionHandle(cancellation);

                EnsureTransaction(session);

                return session;
            }

            private void EnsureTransaction(IClientSessionHandle session)
            {
                Assert(!_abortTransaction);

                if (!session.IsInTransaction)
                {
                    session.StartTransaction();
                }
            }

            public async Task<bool> TryCommitAsync(CancellationToken cancellation = default)
            {
                var session = await GetClientSessionHandle(cancellation);

                _logger.LogTrace($"Committing txn of mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                _operationInProgress = 0; // Volatile write op

                if (_abortTransaction)
                {
                    // Reset this to allocate a new transaction when calling any read/write operation.
                    _abortTransaction = false;
                    Assert(!session.IsInTransaction);
                    return false;
                }

                if (!session.IsInTransaction)
                {
                    return true;
                }

                try
                {
                    await session.CommitTransactionAsync(cancellation);
                    return true;
                }
                catch (MongoCommandException exc) when (exc.Code == 251) // No such transaction (Commit after abort)
                {
                    return false;
                }
                catch (Exception) // TODO: Specify exception type
                {
                    //await session.AbortTransactionAsync();
                    return false;
                }
            }

            public async Task RollbackAsync(CancellationToken cancellation)
            {
                var session = await GetClientSessionHandle(cancellation);

                _logger.LogTrace($"Aborting txn of mongo client session handle '{ session.WrappedCoreSession.Id.ToString()}'.");

                await AbortAsync(session, cancellation);

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

                await session.AbortTransactionAsync(cancellation);

                Assert(!session.IsInTransaction);
            }

            #endregion

            public void Dispose()
            {
                _clientSessionHandle.Dispose();
            }
        }
    }
}
