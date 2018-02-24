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
using MongoDB.Bson.Serialization.Attributes;

namespace AI4E.Storage.MongoDB
{
    public sealed class MongoRouteStoreEntry
    {
        public MongoRouteStoreEntry(string messageType, byte[] route, int ordering)
        {
            MessageType = messageType;
            Route = route;
            Ordering = ordering;
            Id = MongoIdGenerator.GenerateId(MessageType, Convert.ToBase64String(Route));
        }

        private MongoRouteStoreEntry() { }

        [BsonId]
        public string Id { get; internal set; }

        public string MessageType { get; internal set; }
        public byte[] Route { get; internal set; }

        public int Ordering { get; internal set; }
    }
}
