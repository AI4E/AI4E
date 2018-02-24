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
        public static Task<IEnumerable<ICommit<TBucket, TStreamId>>> GetCommitsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence, TBucket bucketId)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, default);
        }

        public static Task<IEnumerable<ICommit<TBucket, TStreamId>>> GetCommitsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(default);
        }

        public static Task<IEnumerable<ICommit<TBucket, TStreamId>>> GetCommitsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                                         TBucket bucketId,
                                                                                                         TStreamId streamId,
                                                                                                         long minRevision = 0,
                                                                                                         CancellationToken cancellation = default)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, minRevision, -1, cancellation);
        }

        public static Task<IEnumerable<ICommit<TBucket, TStreamId>>> GetCommitsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                                         TBucket bucketId,
                                                                                                         TStreamId streamId,
                                                                                                         CancellationToken cancellation = default)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, 0, -1, cancellation);
        }

        public static Task<IEnumerable<ICommit<TBucket, TStreamId>>> GetCommitsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                                         TBucket bucketId,
                                                                                                         TStreamId streamId,
                                                                                                         long minRevision = 0)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, minRevision, -1, default);
        }

        public static Task<IEnumerable<ICommit<TBucket, TStreamId>>> GetCommitsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                                         TBucket bucketId,
                                                                                                         TStreamId streamId)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetCommitsAsync(bucketId, streamId, 0, -1, default);
        }

        public static Task<ISnapshot<TBucket, TStreamId>> GetSnapshotAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                               TBucket bucketId,
                                                                                               TStreamId streamId,
                                                                                               CancellationToken cancellation = default)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotAsync(bucketId, streamId, -1, cancellation);
        }

        public static Task<ISnapshot<TBucket, TStreamId>> GetSnapshotAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                               TBucket bucketId,
                                                                                               TStreamId streamId)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotAsync(bucketId, streamId, -1, default);
        }

        public static Task<IEnumerable<ISnapshot<TBucket, TStreamId>>> GetSnapshotsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence,
                                                                                                             TBucket bucketId)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotsAsync(bucketId, default);
        }

        public static Task<IEnumerable<ISnapshot<TBucket, TStreamId>>> GetSnapshotsAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetSnapshotsAsync(default);
        }

        public static Task<IEnumerable<IStreamHead<TBucket, TStreamId>>> GetStreamsToSnapshotAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence, TBucket bucketId, long maxThreshold)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetStreamsToSnapshotAsync(bucketId, maxThreshold, default);
        }

        public static Task<IEnumerable<IStreamHead<TBucket, TStreamId>>> GetStreamsToSnapshotAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence, long maxThreshold)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetStreamsToSnapshotAsync(maxThreshold, default);
        }

        public static Task<IEnumerable<IStreamHead<TBucket, TStreamId>>> GetStreamsToSnapshotAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence, TBucket bucketId)
           where TBucket : IEquatable<TBucket>
           where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetStreamsToSnapshotAsync(bucketId, 0, default);
        }

        public static Task<IEnumerable<IStreamHead<TBucket, TStreamId>>> GetStreamsToSnapshotAsync<TBucket, TStreamId>(this IStreamPersistence<TBucket, TStreamId> persistence)
            where TBucket : IEquatable<TBucket>
            where TStreamId : IEquatable<TStreamId>
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            return persistence.GetStreamsToSnapshotAsync(0, default);
        }
    }
}
