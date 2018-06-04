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
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static System.Diagnostics.Debug;
using static AI4E.Storage.MongoDB.MongoWriteHelper;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoDatabase : IQueryableDatabase
    {
        private readonly ConcurrentDictionary<Type, object> _collections = new ConcurrentDictionary<Type, object>();
        private readonly IMongoDatabase _database;

        public MongoDatabase(IMongoDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        private IMongoCollection<TEntry> GetCollection<TEntry>()
        {
            return (IMongoCollection<TEntry>)_collections.GetOrAdd(typeof(TEntry), _ => CreateCollection<TEntry>());
        }

        private IMongoCollection<TEntry> CreateCollection<TEntry>()
        {
            // TODO: It is possible that this is called multiple times.
            BsonClassMap.RegisterClassMap<TEntry>(map =>
            {
                map.AutoMap();
                map.MapIdMember(DataPropertyHelper.GetIdMember<TEntry>());
            });

            return _database.GetCollection<TEntry>("data-store." + typeof(TEntry).FullName);
        }

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

        #region IDatabase

        public async ValueTask<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation = default) where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var collection = GetCollection<TEntry>();

            try
            {
                await TryWriteOperation(() => collection.InsertOneAsync(entry, new InsertOneOptions { }, cancellation));
            }
            catch (ConcurrencyException) // TODO: This should be a DuplicateKeyException
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
                var result = await GetAsync(entry, cancellation);

                if (result != null)
                    return result;
            }

            return entry;
        }

        public async ValueTask<bool> RemoveAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var collection = GetCollection<TEntry>();
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
            var collection = GetCollection<TEntry>();

            var deleteManyResult = await TryWriteOperation(() => collection.DeleteManyAsync(p => true, cancellation));

            if (!deleteManyResult.IsAcknowledged)
            {
                throw new StorageException();
            }
        }

        public async ValueTask<bool> UpdateAsync<TEntry>(TEntry entry, Expression<Func<TEntry, bool>> predicate, CancellationToken cancellation = default) where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var collection = GetCollection<TEntry>();
            var updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(BuildPredicate(entry, predicate),
                                                                                        entry,
                                                                                        options: new UpdateOptions { IsUpsert = false },
                                                                                        cancellationToken: cancellation));

            if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 && updateResult.UpsertedId == null)
            {
                throw new StorageException();
            }

            Assert(updateResult.MatchedCount == 0 || updateResult.MatchedCount == 1);

            return updateResult.MatchedCount != 0; // TODO: What does the result value represent? Matched count or modified count.
        }



        public ValueTask<bool> CompareExchangeAsync<TEntry>(TEntry entry,
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

        private async ValueTask<bool> CheckComparandToBeUpToDate<TEntry>(TEntry comparand,
                                                                         Expression<Func<TEntry, TEntry, bool>> equalityComparer,
                                                                         CancellationToken cancellation)
            where TEntry : class
        {
            var result = await GetAsync(comparand, cancellation);

            if (comparand == null)
            {
                return result == null;
            }

            if (result == null)
                return false;

            return equalityComparer.Compile(preferInterpretation: true).Invoke(comparand, result);
        }

        private ValueTask<TEntry> GetAsync<TEntry>(TEntry comparand, CancellationToken cancellation)
            where TEntry : class
        {
            Assert(comparand != null);
            var predicate = DataPropertyHelper.BuildPredicate(comparand);
            return GetSingleAsync(predicate, cancellation);
        }

        public ValueTask<TEntry> GetAsync<TId, TEntry>(TId id, CancellationToken cancellation = default)
            where TId : IEquatable<TId>
            where TEntry : class
        {
            if (id == null)
                return new ValueTask<TEntry>(default(TEntry));

            var predicate = DataPropertyHelper.BuildPredicate<TId, TEntry>(id);
            return GetSingleAsync(predicate, cancellation);
        }

        public IAsyncEnumerable<TEntry> GetAllAsync<TEntry>(CancellationToken cancellation = default)
            where TEntry : class
        {
            var collection = GetCollection<TEntry>();
            return new MongoQueryEvaluator<TEntry>(collection, p => true, cancellation);
        }

        #endregion

        #region IFilterableDatabase

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(Expression<Func<TEntry, bool>> predicate,
                                                         CancellationToken cancellation = default)
           where TEntry : class
        {
            var collection = GetCollection<TEntry>();
            return new MongoQueryEvaluator<TEntry>(collection, predicate, cancellation);
        }

        public async ValueTask<TEntry> GetSingleAsync<TEntry>(Expression<Func<TEntry, bool>> predicate,
                                                              CancellationToken cancellation = default)
            where TEntry : class
        {
            var collection = GetCollection<TEntry>();

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
            var cursorSource = ((IMongoQueryable<TResult>)queryShaper(GetCollection<TEntry>().AsQueryable()));
            var queryEvaluator = new MongoQueryEvaluator<TResult>(cursorSource, cancellation);

            return queryEvaluator;
        }

        #endregion
    }
}
