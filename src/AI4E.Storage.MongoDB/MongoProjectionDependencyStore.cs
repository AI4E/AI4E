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
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static AI4E.Storage.MongoDB.MongoWriteHelper;

namespace AI4E.Storage.MongoDB
{
    [Obsolete]
    public sealed class MongoProjectionDependencyStore<TBucketId, TStreamId> : IProjectionDependencyStore<TBucketId, TStreamId>
       where TBucketId : IEquatable<TBucketId>
       where TStreamId : IEquatable<TStreamId>
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<MongoProjectionDependencyMap<TBucketId, TStreamId>> _collection;

        public MongoProjectionDependencyStore(IMongoDatabase database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
            _collection = _database.GetCollection<MongoProjectionDependencyMap<TBucketId, TStreamId>>("projection-dependencies");
        }

        public async Task AddDependencyAsync(TBucketId dependentBucketId, TStreamId dependentStreamId, TBucketId dependencyBucketId, TStreamId dependencyStreamId, CancellationToken cancellation = default)
        {
            var data = new MongoProjectionDependencyMap<TBucketId, TStreamId>
            {
                Dependency = new MongoProjectionDependency<TBucketId, TStreamId>
                {
                    BucketId = dependencyBucketId,
                    Id = dependencyStreamId
                },
                Dependent = new MongoProjectionDependency<TBucketId, TStreamId>
                {
                    BucketId = dependentBucketId,
                    Id = dependentStreamId
                }
            };

            var updateResult = await TryWriteOperation(() => _collection.ReplaceOneAsync(p => p.Dependent.BucketId.Equals(dependentBucketId) &&
                                                                                             p.Dependent.Id.Equals(dependentStreamId) &&
                                                                                             p.Dependency.BucketId.Equals(dependencyBucketId) &&
                                                                                             p.Dependency.Id.Equals(dependencyStreamId),
                                                                                         data,
                                                                                         options: new UpdateOptions { IsUpsert = true },
                                                                                         cancellationToken: cancellation));

            if (!updateResult.IsAcknowledged || updateResult.MatchedCount == 0 && updateResult.UpsertedId == null)
            {
                throw new StorageException();
            }
        }

        public async Task<IEnumerable<IProjectionDependency<TBucketId, TStreamId>>> GetDependenciesAsync(TBucketId bucketId, TStreamId streamId, CancellationToken cancellation = default)
        {
            return await _collection.AsQueryable().Where(p => p.Dependent.BucketId.Equals(bucketId) && p.Dependent.Id.Equals(streamId)).Select(p => p.Dependency).ToListAsync();
        }

        public async Task<IEnumerable<IProjectionDependency<TBucketId, TStreamId>>> GetDependentsAsync(TBucketId bucketId, TStreamId streamId, CancellationToken cancellation = default)
        {
            return await _collection.AsQueryable().Where(p => p.Dependency.BucketId.Equals(bucketId) && p.Dependency.Id.Equals(streamId)).Select(p => p.Dependent).ToListAsync();
        }

        public async Task RemoveDependencyAsync(TBucketId dependentBucketId, TStreamId dependentStreamId, TBucketId dependencyBucketId, TStreamId dependencyStreamId, CancellationToken cancellation = default)
        {
            var deleteResult = await TryWriteOperation(() => _collection.DeleteOneAsync(p => p.Dependent.BucketId.Equals(dependentBucketId) &&
                                                                                             p.Dependent.Id.Equals(dependentStreamId) &&
                                                                                             p.Dependency.BucketId.Equals(dependencyBucketId) &&
                                                                                             p.Dependency.Id.Equals(dependencyStreamId), cancellationToken: cancellation));

            if (!deleteResult.IsAcknowledged || deleteResult.DeletedCount == 0)
            {
                throw new StorageException();
            }
        }
    }

    [Obsolete]
    public sealed class MongoProjectionDependency<TBucketId, TStreamId> : IProjectionDependency<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        public TStreamId Id { get; set; }
        public TBucketId BucketId { get; set; }
    }

    [Obsolete]
    public sealed class MongoProjectionDependencyMap<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        private string _id;

        [BsonId]
        public string Id
        {
            get
            {
                if (_id == null)
                {
                    _id = MongoIdGenerator.GenerateId(Dependency.BucketId, Dependency.Id, Dependent.BucketId, Dependent.Id);
                }

                return _id;
            }
            set => _id = value;
        }

        public MongoProjectionDependency<TBucketId, TStreamId> Dependency { get; set; }
        public MongoProjectionDependency<TBucketId, TStreamId> Dependent { get; set; }
    }
}
