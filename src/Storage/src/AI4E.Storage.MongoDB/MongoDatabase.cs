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
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static AI4E.Storage.MongoDB.MongoExceptionHelper;

namespace AI4E.Storage.MongoDB
{
    public sealed partial class MongoDatabase : IDatabase
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<MongoDatabase> _logger;

        public MongoDatabase(IMongoDatabase database, ILoggerFactory? loggerFactory = null)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            UnderlyingDatabase = database;
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<MongoDatabase>();

            _counterCollection = UnderlyingDatabase
                .GetCollection<CounterEntry>(_counterEntryCollectionName);

            _collectionLookupCollection = UnderlyingDatabase
                .GetCollection<CollectionLookupEntry>(_collectionLookupCollectionName);
        }

        internal IMongoDatabase UnderlyingDatabase { get; }

        bool IDatabase.SupportsScopes => true;

        private static Expression<Func<TEntry, bool>> BuildPredicate<TEntry>(
            TEntry comparand,
            Expression<Func<TEntry, bool>> predicate)
        {
            Debug.Assert(comparand != null);
            Debug.Assert(predicate != null);

            var parameter = predicate!.Parameters.First();
            var idSelector = DataPropertyHelper.BuildPredicate(comparand);

            var body = Expression.AndAlso(
                ParameterExpressionReplacer.ReplaceParameter(idSelector.Body, idSelector.Parameters.First(), parameter),
                predicate.Body);

            return Expression.Lambda<Func<TEntry, bool>>(body, parameter);
        }

        private static readonly InsertOneOptions _defaultInsertOneOptions = new InsertOneOptions();

        public async ValueTask<bool> AddAsync<TEntry>(TEntry entry, CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var collection = await GetCollectionAsync<TEntry>(cancellation).ConfigureAwait(false);
            var idSelector = DataPropertyHelper.BuildPredicate(entry);

            using (var cursor = await collection
                .FindAsync(idSelector, new FindOptions<TEntry, TEntry> { Limit = 1 }, cancellation)
                .ConfigureAwait(false))
            {
                if (await cursor.FirstOrDefaultAsync(cancellation).ConfigureAwait(false) != null)
                {
                    return false;
                }
            }

            try
            {
                await TryOperation(() => collection.InsertOneAsync(entry, _defaultInsertOneOptions, cancellation))
                    .ConfigureAwait(false);
            }
            catch (DuplicateKeyException)
            {
                return false;
            }

            return true;
        }

        public async ValueTask<bool> RemoveAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var collection = await GetCollectionAsync<TEntry>(cancellation).ConfigureAwait(false);
            var deleteResult = await TryWriteOperation(() => collection.DeleteOneAsync(
                BuildPredicate(entry, predicate),
                cancellationToken: cancellation)).ConfigureAwait(false);

            if (!deleteResult.IsAcknowledged)
            {
                throw new StorageException();
            }

            Debug.Assert(deleteResult.DeletedCount == 0 || deleteResult.DeletedCount == 1);

            return deleteResult.DeletedCount > 0;
        }

        public async ValueTask Clear<TEntry>(CancellationToken cancellation = default)
             where TEntry : class
        {
            var collection = await GetCollectionAsync<TEntry>(cancellation).ConfigureAwait(false);

            var deleteManyResult = await TryWriteOperation(() => collection.DeleteManyAsync(p => true, cancellation))
                .ConfigureAwait(false);

            if (!deleteManyResult.IsAcknowledged)
            {
                throw new StorageException();
            }
        }

        public async ValueTask<bool> UpdateAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var collection = await GetCollectionAsync<TEntry>(cancellation).ConfigureAwait(false);
            var updateResult = await TryWriteOperation(() => collection.ReplaceOneAsync(
                BuildPredicate(entry, predicate),
                entry,
                options: new UpdateOptions { IsUpsert = false },
                cancellationToken: cancellation)).ConfigureAwait(false);

            if (!updateResult.IsAcknowledged)
            {
                throw new StorageException();
            }

            Debug.Assert(updateResult.MatchedCount == 0 || updateResult.MatchedCount == 1);

            // TODO: What does the result value represent? Matched count or modified count.
            return updateResult.MatchedCount != 0;
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            var collection = GetCollectionAsync<TEntry>(cancellation);
            return new MongoQueryEvaluator<TEntry>(collection!, predicate);
        }

        public async ValueTask<TEntry?> GetOneAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            var collection = await GetCollectionAsync<TEntry>(cancellation)
                .ConfigureAwait(false);

            using var cursor = await collection
                .FindAsync(predicate, new FindOptions<TEntry, TEntry> { Limit = 1 }, cancellation)
                .ConfigureAwait(false);

            return await cursor.FirstOrDefaultAsync(cancellation).ConfigureAwait(false);
        }

        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper,
            CancellationToken cancellation = default)
            where TEntry : class
        {
            if (queryShaper is null)
                throw new ArgumentNullException(nameof(queryShaper));

            async ValueTask<IAsyncCursorSource<TResult>> GetCursorSourceAsync()
            {
                var collection = await GetCollectionAsync<TEntry>(cancellation)
                    .ConfigureAwait(false);

                return (IMongoQueryable<TResult>)queryShaper(collection.AsQueryable());
            }

            return new MongoQueryEvaluator<TResult>(GetCursorSourceAsync());
        }

        public IDatabaseScope CreateScope()
        {
            return new MongoDatabaseScope(this, _loggerFactory.CreateLogger<MongoDatabaseScope>());
        }
    }
}
