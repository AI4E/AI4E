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
using AI4E.Remoting;
using AI4E.Routing;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace AI4E.Storage.MongoDB
{
    [Obsolete]
    public sealed class MongoRouteMap<TAddress> : IRouteMap<TAddress>
    {
        private readonly IMongoDatabase _database;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly IMongoCollection<MongoRouteMapEntry> _collection;

        public MongoRouteMap(IMongoDatabase database, IAddressConversion<TAddress> addressConversion)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _database = database;
            _addressConversion = addressConversion;

            _collection = _database.GetCollection<MongoRouteMapEntry>("route-map");
        }

        public async Task<IEnumerable<TAddress>> GetMapsAsync(EndPointRoute endPoint, CancellationToken cancellation)
        {
            var routeString = endPoint.ToString();
            var entries = await _collection.AsQueryable()
                                     .Where(p => p.Route == routeString)
                                     .ToListAsync(cancellation);

            return entries.Select(p => _addressConversion.Parse(p.Address));
        }

        public async Task MapRouteAsync(EndPointRoute localEndPoint, TAddress address, CancellationToken cancellation)
        {
            var routeString = localEndPoint.ToString();
            var addressString = _addressConversion.ToString(address);

            var replaceResult = await MongoWriteHelper.TryWriteOperation(
                () => _collection.ReplaceOneAsync(entry => entry.Address == addressString && entry.Route == routeString,
                                                  new MongoRouteMapEntry(addressString, routeString),
                                                  new UpdateOptions { IsUpsert = true },
                                                  cancellation));
        }

        public async Task UnmapRouteAsync(EndPointRoute localEndPoint, TAddress address, CancellationToken cancellation)
        {
            var routeString = localEndPoint;
            var addressString = _addressConversion.ToString(address);

            var deleteResult = await MongoWriteHelper.TryWriteOperation(() => _collection.DeleteOneAsync(entry => entry.Route == addressString && entry.Address == addressString, cancellation));
        }

        public Task UnmapRouteAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            var routeString = localEndPoint.ToString();

            return MongoWriteHelper.TryWriteOperation(() => _collection.DeleteManyAsync(entry => entry.Route == routeString, cancellation));
        }
    }

    public sealed class MongoRouteMapEntry
    {
        public MongoRouteMapEntry(string address, string route)
        {
            Address = address;
            Route = route;

            Id = MongoIdGenerator.GenerateId(address, route);
        }

        internal MongoRouteMapEntry() { }

        [BsonId]
        public string Id { get; internal set; }

        public string Address { get; internal set; }

        public string Route { get; internal set; }
    }
}
