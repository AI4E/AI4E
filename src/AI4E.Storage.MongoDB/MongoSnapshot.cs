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
    internal sealed class MongoSnapshot<TBucket, TStreamId> : ISnapshot<TBucket, TStreamId>
        where TBucket : IEquatable<TBucket>
        where TStreamId : IEquatable<TStreamId>  
    {
        private string _id;

        public MongoSnapshot(
            TBucket bucketId,
            TStreamId streamId,
            long streamRevision,
            object payload,
            Guid concurrencyToken,
            IReadOnlyDictionary<string, object> headers)
        {
            BucketId = bucketId;
            StreamId = streamId;
            StreamRevision = streamRevision;
            Payload = payload;
            ConcurrencyToken = concurrencyToken;

            foreach (var entry in headers)
            {
                Headers.Add(entry.Key, entry.Value);
            }
        }

        public MongoSnapshot(ISnapshot<TBucket, TStreamId> snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            BucketId = snapshot.BucketId;
            StreamId = snapshot.StreamId;
            StreamRevision = snapshot.StreamRevision;
            Payload = snapshot.Payload;
            ConcurrencyToken = snapshot.ConcurrencyToken;

            foreach (var entry in snapshot.Headers)
            {
                Headers.Add(entry.Key, entry.Value);
            }
        }

        private MongoSnapshot() { }

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

        public TBucket BucketId { get; private set; }

        public TStreamId StreamId { get; private set; }

        public long StreamRevision { get; private set; }

        public object Payload { get; private set; }

        public Guid ConcurrencyToken { get; private set; }

        IReadOnlyDictionary<string, object> ISnapshot<TBucket, TStreamId>.Headers => Headers ?? (IReadOnlyDictionary<string, object>)ImmutableDictionary<string, object>.Empty;

        Dictionary<string, object> Headers { get; } = new Dictionary<string, object>();
    }
}
