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
    internal sealed class MongoStreamHead<TBucketId, TStreamId> : IStreamHead<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        private string _id;

        public MongoStreamHead(TBucketId bucketId, TStreamId streamId, long headRevision, long snapshotRevision, long dispatchedRevision)
        {
            BucketId = bucketId;
            StreamId = streamId;
            HeadRevision = headRevision;
            DispatchedRevision = dispatchedRevision;
            SnapshotRevision = snapshotRevision;
            HeadRevisionAdvance = headRevision - snapshotRevision;
            IsDeleted = false;
        }

        private MongoStreamHead() { }

        [BsonId]
        public string Id
        {
            get
            {
                if (_id == null)
                {
                    _id = MongoIdGenerator.GenerateId(BucketId, StreamId);
                }

                return _id;
            }
            private set => _id = value;
        }

        public TBucketId BucketId { get; private set; }

        public TStreamId StreamId { get; private set; }

        public long HeadRevision { get; set; }

        public long SnapshotRevision { get; set; }

        public long DispatchedRevision { get; set; }

        public long HeadRevisionAdvance { get; set; }

        public bool IsDeleted { get; set; }
    }
}
