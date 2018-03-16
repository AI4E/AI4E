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

namespace AI4E.Storage
{
    public static class StreamPersistenceExtension
    {
        public static Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence, TBucketId bucketId)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, default);
        }

        public static Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(default);
        }

        public static Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                                         TBucketId bucketId,
                                                                                                         TStreamId streamId,
                                                                                                         long minRevision,
                                                                                                         CancellationToken cancellation = default)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, minRevision, maxRevision: default, cancellation);
        }

        public static Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                                         TBucketId bucketId,
                                                                                                         TStreamId streamId,
                                                                                                         CancellationToken cancellation = default)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, minRevision: default, maxRevision: default, cancellation);
        }

        public static Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                                         TBucketId bucketId,
                                                                                                         TStreamId streamId,
                                                                                                         long minRevision)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, minRevision, maxRevision: default, default);
        }

        public static Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                                         TBucketId bucketId,
                                                                                                         TStreamId streamId)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, minRevision: default, maxRevision: default, default);
        }

        public static Task<ISnapshot<TBucketId, TStreamId>> GetSnapshotAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                               TBucketId bucketId,
                                                                                               TStreamId streamId,
                                                                                               CancellationToken cancellation = default)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotAsync(bucketId, streamId, maxRevision: default, cancellation);
        }

        public static Task<ISnapshot<TBucketId, TStreamId>> GetSnapshotAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                               TBucketId bucketId,
                                                                                               TStreamId streamId)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotAsync(bucketId, streamId, maxRevision: default, default);
        }

        public static Task<IEnumerable<ISnapshot<TBucketId, TStreamId>>> GetSnapshotsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence,
                                                                                                             TBucketId bucketId)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotsAsync(bucketId, default);
        }

        public static Task<IEnumerable<ISnapshot<TBucketId, TStreamId>>> GetSnapshotsAsync<TBucketId, TStreamId>(this IStreamPersistence<TBucketId, TStreamId> persistence)
            where TBucketId : IEquatable<TBucketId>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotsAsync(default);
        }
    }
}
