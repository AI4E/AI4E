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
using MongoDB.Bson.Serialization.Attributes;

namespace AI4E.Storage.MongoDB
{
    internal class MongoCommit<TBucketId, TStreamId> : ICommit<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        private string _id;

        public MongoCommit(
            TBucketId bucketId,
            TStreamId streamId,
            Guid concurrencyToken,
            long streamRevision,
            DateTime commitStamp,
            IReadOnlyDictionary<string, object> headers,
            object body,
            IReadOnlyCollection<EventMessage> events,
            bool isDispatched)
        {
            BucketId = bucketId;
            StreamId = streamId;
            ConcurrencyToken = concurrencyToken;
            StreamRevision = streamRevision;
            CommitStamp = commitStamp;

            foreach (var entry in headers)
            {
                Headers.Add(entry.Key, entry.Value);
            }

            Body = body;
            Events = new List<EventMessage>(events);
            IsDispatched = isDispatched;
        }

        private MongoCommit() { }

        [BsonId]
        public string Id
        {
            get
            {
                if (_id == null)
                {
                    _id = MongoIdGenerator.GenerateId(BucketId, StreamId, StreamRevision);
                }

                return _id;
            }
            private set => _id = value;
        }

        public TBucketId BucketId { get; private set; }

        public TStreamId StreamId { get; private set; }

        public Guid ConcurrencyToken { get; private set; }

        public long StreamRevision { get; private set; }

        public DateTime CommitStamp { get; private set; }

        IReadOnlyDictionary<string, object> ICommit<TBucketId, TStreamId>.Headers => Headers ?? (IReadOnlyDictionary<string, object>)ImmutableDictionary<string, object>.Empty;

        public Dictionary<string, object> Headers { get; } = new Dictionary<string, object>();

        public object Body { get; private set; }

        IReadOnlyCollection<EventMessage> ICommit<TBucketId, TStreamId>.Events => Events ?? (IReadOnlyCollection<EventMessage>)ImmutableList<EventMessage>.Empty;

        public List<EventMessage> Events { get; } = new List<EventMessage>();

        public bool IsDispatched { get; set; }

        public bool IsDeleted { get; set; }
    }
}
