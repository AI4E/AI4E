/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        MongoDatabase.cs 
 * Types:           (1) AI4E.Storage.MongoDB.MongoDatabase
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.06.2018 
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Storage.Transactions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;
using static AI4E.Storage.MongoDB.MongoWriteHelper;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoDatabase : IQueryableDatabase, ITransactionalDatabase
    {
        private const string _collectionLookupEntryResourceKey = "collection-lookup-entry";
        private const string _counterEntryCollectionName = "data-store-counter";
        private const string _collectionLookupCollectionName = "data-store-collection-lookup";

        #region Fields

        private readonly IMongoDatabase _database;

        private readonly ConcurrentDictionary<Type, object> _collections = new ConcurrentDictionary<Type, object>();

        private readonly IMongoCollection<CounterEntry> _counterCollection;
        private readonly IMongoCollection<CollectionLookupEntry> _collectionLookupCollection;

        #endregion

        #region C'tor

        static MongoDatabase()
        {
            BsonSerializer.RegisterSerializationProvider(new StructSerializationProvider());
        }

        public MongoDatabase(IMongoDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;

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

        private ValueTask<IMongoCollection<TEntry>> GetCollectionAsync<TEntry>(CancellationToken cancellation)
        {
            var lookup = (CollectionLookup<TEntry>)_collections.GetOrAdd(typeof(TEntry), _ => BuildCollectionLookup<TEntry>());

            Assert(lookup != null);

            return lookup.GetCollectionAsync(cancellation);
        }

        private CollectionLookup<TEntry> BuildCollectionLookup<TEntry>()
        {
            return new CollectionLookup<TEntry>(this);
        }

        private static string GetCollectionKey<TEntry>()
        {
            return typeof(TEntry).ToString();
        }

        private async Task<string> GetCollectionNameAsync(string collectionKey, CancellationToken cancellation)
        {
            var lookupEntry = await _collectionLookupCollection.AsQueryable()
                                                               .FirstOrDefaultAsync(p => p.CollectionKey == collectionKey);

            if (lookupEntry != null)
            {
                return lookupEntry.CollectionName;
            }

            // There is no collection entry. We have to allocate one. Get a unique key.
            var collectionId = await GetUniqueResourceIdAsync(_collectionLookupEntryResourceKey, cancellation);

            try
            {
                lookupEntry = new CollectionLookupEntry { CollectionKey = collectionKey, CollectionName = "data-store#" + collectionId };
                await TryWriteOperation(() => _collectionLookupCollection.InsertOneAsync(lookupEntry, default, cancellation));
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

        private async Task<IMongoCollection<TEntry>> GetCollectionAsync<TEntry>(string collectionName)
        {
            BsonClassMap.RegisterClassMap<TEntry>(map =>
            {
                map.AutoMap();
                map.MapIdMember(DataPropertyHelper.GetIdMember<TEntry>());
            });

            if (!await CollectionExistsAsync(collectionName))
            {
                _database.CreateCollection(collectionName);
            }

            return _database.GetCollection<TEntry>(collectionName);
        }

        private sealed class CollectionLookupEntry
        {
            [BsonId]
            public string CollectionKey { get; set; }

            public string CollectionName { get; set; }
        }

        private sealed class CollectionLookup<TEntry>
        {
            private readonly MongoDatabase _database;

            private volatile IMongoCollection<TEntry> _collection = null;
            private readonly AsyncLock _lock = new AsyncLock();

            public CollectionLookup(MongoDatabase database)
            {
                Assert(database != null);

                _database = database;
            }

            public async ValueTask<IMongoCollection<TEntry>> GetCollectionAsync(CancellationToken cancellation)
            {
                // Volatile read op.
                var collection = _collection;

                if (collection != null)
                {
                    return collection;
                }

                using (await _lock.LockAsync(cancellation))
                {
                    // Volatile read op.
                    collection = _collection;

                    if (collection != null)
                    {
                        return collection;
                    }


                    var collectionKey = GetCollectionKey<TEntry>();
                    var collectionName = await _database.GetCollectionNameAsync(collectionKey, cancellation);

                    // Volatile write op.
                    _collection = collection = await _database.GetCollectionAsync<TEntry>(collectionName);

                    Assert(collection != null);
                    return collection;
                }
            }
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

        private static Expression<Func<TEntry, bool>> BuildPredicate<TEntry>(TEntry comparand,
                                                                             Expression<Func<TEntry, TEntry, bool>> equalityComparer)
        {
            Assert(comparand != null);
            Assert(equalityComparer != null);

            var idSelector = DataPropertyHelper.BuildPredicate(comparand);
            var comparandConstant = Expression.Constant(comparand, typeof(TEntry));
            var parameter = equalityComparer.Parameters.First();
            var equality = ParameterExpressionReplacer.ReplaceParameter(equalityComparer.Body, equalityComparer.Parameters.Last(), comparandConstant);
            var idEquality = ParameterExpressionReplacer.ReplaceParameter(idSelector.Body, idSelector.Parameters.First(), parameter);
            var body = Expression.AndAlso(idEquality, equality);

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

        public async ValueTask<TEntry> GetOrAdd<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            while (!await AddAsync(entry, cancellation))
            {
                var result = await GetOneAsync(entry, cancellation);

                if (result != null)
                    return result;
            }

            return entry;
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

        public Task<bool> CompareExchangeAsync<TEntry>(TEntry entry,
                                                       TEntry comparand,
                                                       Expression<Func<TEntry, TEntry, bool>> equalityComparer,
                                                       CancellationToken cancellation = default) where TEntry : class
        {
            if (equalityComparer == null)
                throw new ArgumentNullException(nameof(equalityComparer));

            // This is a nop actually. But we check whether comparand is up to date.
            if (entry == comparand)
            {
                return CheckComparandToBeUpToDate(comparand, equalityComparer, cancellation);
            }

            // Trying to update an entry.
            if (entry != null && comparand != null)
            {
                return UpdateAsync(entry, BuildPredicate(comparand, equalityComparer), cancellation);
            }

            // Trying to create an entry.
            if (entry != null)
            {
                return AddAsync(entry, cancellation);
            }

            // Trying to remove an entry.
            Assert(comparand != null);

            return RemoveAsync(comparand, BuildPredicate(comparand, equalityComparer), cancellation);
        }

        private async Task<bool> CheckComparandToBeUpToDate<TEntry>(TEntry comparand,
                                                                    Expression<Func<TEntry, TEntry, bool>> equalityComparer,
                                                                    CancellationToken cancellation)
            where TEntry : class
        {
            var result = await GetOneAsync(comparand, cancellation);

            if (comparand == null)
            {
                return result == null;
            }

            if (result == null)
                return false;

            return equalityComparer.Compile(preferInterpretation: true).Invoke(comparand, result);
        }

        private ValueTask<TEntry> GetOneAsync<TEntry>(TEntry comparand, CancellationToken cancellation)
            where TEntry : class
        {
            Assert(comparand != null);
            var predicate = DataPropertyHelper.BuildPredicate(comparand);
            return GetOneAsync(predicate, cancellation);
        }

        public ValueTask<TEntry> GetOneAsync<TId, TEntry>(TId id, CancellationToken cancellation = default)
            where TId : IEquatable<TId>
            where TEntry : class
        {
            if (id == null)
                return new ValueTask<TEntry>(default(TEntry));

            var predicate = DataPropertyHelper.BuildPredicate<TId, TEntry>(id);
            return GetOneAsync(predicate, cancellation);
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(CancellationToken cancellation = default)
            where TEntry : class
        {
            var collection = GetCollectionAsync<TEntry>(cancellation);
            return new MongoQueryEvaluator<TEntry>(collection, p => true, cancellation);
        }

        #endregion

        #region IFilterableDatabase

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

        #region ITransactionalDatabase

        public IScopedTransactionalDatabase CreateScope()
        {
            return new MongoScopedTransactionalDatabase(this);
        }

        #endregion

        private sealed class MongoScopedTransactionalDatabase : IScopedTransactionalDatabase
        {
            private bool _isTransactionInProgress = false;

            private readonly MongoDatabase _owner;
            private readonly DisposableAsyncLazy<IClientSessionHandle> _clientSessionHandle;


            public MongoScopedTransactionalDatabase(MongoDatabase owner)
            {
                _owner = owner;
                _clientSessionHandle = new DisposableAsyncLazy<IClientSessionHandle>(CreateClientSessionHandle, DisposeClientSessionHandle, DisposableAsyncLazyOptions.ExecuteOnCallingThread);
            }

            #region ClientSessionHandle

            private Task<IClientSessionHandle> GetClientSessionHandle(CancellationToken cancellation)
            {
                return _clientSessionHandle.Task.WithCancellation(cancellation);
            }

            private Task<IClientSessionHandle> CreateClientSessionHandle(CancellationToken cancellation)
            {
                var mongoClient = _owner._database.Client;
                return mongoClient.StartSessionAsync(cancellationToken: cancellation);
            }

            private async Task DisposeClientSessionHandle(IClientSessionHandle clientSessionHandle)
            {
                Assert(clientSessionHandle != null);
                try
                {
                    await clientSessionHandle.AbortTransactionAsync();
                }
                finally
                {
                    clientSessionHandle.Dispose();
                }
            }

            #endregion

            #region IScopedTransactionalDatabase

            public async Task StoreAsync<TData>(TData data, CancellationToken cancellation = default)
                where TData : class
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                var session = await EnsureTransactionAsync(cancellation);
                var predicate = DataPropertyHelper.BuildPredicate(data);
                var collection = await _owner.GetCollectionAsync<TData>(cancellation);

                ReplaceOneResult updateResult;

                try
                {
                    updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(session, predicate, data, options: new UpdateOptions { IsUpsert = true }, cancellationToken: cancellation));
                }
                catch (MongoCommandException exc) when (exc.Code == 112) // Write conflict.
                {
                    throw new TransactionAbortedException();
                }

                if (!updateResult.IsAcknowledged)
                {
                    throw new StorageException();
                }

                Assert(updateResult.MatchedCount == 0 || updateResult.MatchedCount == 1);
            }

            public async Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default)
                where TData : class
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                var session = await EnsureTransactionAsync(cancellation);
                var predicate = DataPropertyHelper.BuildPredicate(data);
                var collection = await _owner.GetCollectionAsync<TData>(cancellation);

                DeleteResult deleteResult;

                try
                {
                    deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(session, predicate, cancellationToken: cancellation));
                }
                catch (MongoCommandException exc) when (exc.Code == 112) // Write conflict.
                {
                    throw new TransactionAbortedException();
                }

                if (!deleteResult.IsAcknowledged)
                {
                    throw new StorageException();
                }

                Assert(deleteResult.DeletedCount == 0 || deleteResult.DeletedCount == 1);
            }

            public IAsyncEnumerable<TData> GetAsync<TData>(Expression<Func<TData, bool>> predicate, CancellationToken cancellation = default)
                where TData : class
            {
                var sessionFuture = EnsureTransactionAsync(cancellation);
                var collection = _owner.GetCollectionAsync<TData>(cancellation);
                return new MongoQueryEvaluator<TData>(collection, predicate, sessionFuture, cancellation);
            }

            private async Task<IClientSessionHandle> EnsureTransactionAsync(CancellationToken cancellation)
            {
                var session = await GetClientSessionHandle(cancellation);

                if (!_isTransactionInProgress)
                {
                    session.StartTransaction();
                    _isTransactionInProgress = true;
                }

                return session;
            }

            public async Task<bool> TryCommitAsync(CancellationToken cancellation = default)
            {
                if (!_isTransactionInProgress)
                    return true;

                var session = await GetClientSessionHandle(cancellation);

                _isTransactionInProgress = false;

                try
                {
                    await session.CommitTransactionAsync(cancellation);
                    return true;
                }
                catch (MongoCommandException exc) when (exc.Code == 251) // No such transaction (Commit after abort)
                {
                    return false;
                }
                catch (Exception exc) // TODO: Specify exception type
                {
                    //await session.AbortTransactionAsync();
                    return false;
                }
            }

            public async Task RollbackAsync(CancellationToken cancellation = default)
            {
                if (!_isTransactionInProgress)
                    return;

                var session = await GetClientSessionHandle(cancellation);

                _isTransactionInProgress = false;
                await session.AbortTransactionAsync(cancellation);
            }

            #endregion

            public void Dispose()
            {
                _clientSessionHandle.Dispose();
            }
        }
    }
}
