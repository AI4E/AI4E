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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Routing;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoRouteStore : IRouteStore
    {
        private readonly IMongoDatabase _database;
        private readonly IRouteSerializer _routeSerializer;
        private readonly IMongoCollection<MongoRouteStoreEntry> _collection;
        private readonly IMongoCollection<MongoSequenceEntry> _sequence;

        public MongoRouteStore(IMongoDatabase database,
                               IRouteSerializer routeSerializer)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (routeSerializer == null)
                throw new ArgumentNullException(nameof(routeSerializer));

            _database = database;
            _routeSerializer = routeSerializer;
            _collection = _database.GetCollection<MongoRouteStoreEntry>("route-store");
            _sequence = _database.GetCollection<MongoSequenceEntry>("seq-store");
        }

        public async Task<bool> AddRouteAsync(EndPointRoute localRoute, string messageType, CancellationToken cancellation)
        {
            var seq = (await _sequence.FindOneAndUpdateAsync(Builders<MongoSequenceEntry>.Filter.Eq(p => p.Id, "route-store-entry"),
                                                            Builders<MongoSequenceEntry>.Update.Inc(p => p.Seq, 1),
                                                            new FindOneAndUpdateOptions<MongoSequenceEntry, MongoSequenceEntry>
                                                            {
                                                                IsUpsert = true,
                                                                ReturnDocument = ReturnDocument.After
                                                            },
                                                            cancellation)).Seq;

            var serializedMessageType = messageType;
            var serializedRoute = _routeSerializer.SerializeRoute(localRoute);

            var replaceResult = await MongoWriteHelper.TryWriteOperation(
                () => _collection.ReplaceOneAsync(x => x.MessageType == serializedMessageType && x.Route == serializedRoute,
                                                  new MongoRouteStoreEntry(serializedMessageType, serializedRoute, seq),
                                                  new UpdateOptions { IsUpsert = true },
                                                  cancellation));

            return replaceResult.IsAcknowledged && replaceResult.MatchedCount == 0;
        }

        public async Task<bool> RemoveRouteAsync(EndPointRoute localRoute, string messageType, CancellationToken cancellation)
        {
            var res = await MongoWriteHelper.TryWriteOperation(() => _collection.DeleteOneAsync(p => p.MessageType == messageType && p.Route == _routeSerializer.SerializeRoute(localRoute), cancellation));

            if (!res.IsAcknowledged || res.DeletedCount == 0)
            {
                return false;
            }

            return true;
        }

        public Task RemoveRouteAsync(EndPointRoute localRoute, CancellationToken cancellation)
        {
            return MongoWriteHelper.TryWriteOperation(() => _collection.DeleteManyAsync(p => p.Route == _routeSerializer.SerializeRoute(localRoute), cancellation));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            return (await _collection.AsQueryable().Where(p => p.MessageType == messageType).Select(p => p.Route).ToListAsync(cancellation)).Select(p => _routeSerializer.DeserializeRoute(p));
        }

        public async Task<IEnumerable<EndPointRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            return (await _collection.AsQueryable().Select(p => p.Route).ToListAsync(cancellation)).Select(p => _routeSerializer.DeserializeRoute(p));
        }
    }
}
