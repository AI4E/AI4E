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
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using static AI4E.Storage.MongoDB.MongoWriteHelper;

namespace AI4E.Storage.MongoDB
{
    [Obsolete]
    public sealed class MongoStreamPersistence<TBucketId, TStreamId> : IStreamPersistence<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        private readonly IMongoDatabase _database;
        private readonly ISnapshotProcessor<TBucketId, TStreamId> _snapshotProcessor;
        private readonly IMongoCollection<MongoCommit<TBucketId, TStreamId>> _commits;
        private readonly IMongoCollection<MongoStreamHead<TBucketId, TStreamId>> _streamHeads;
        private readonly IMongoCollection<MongoSnapshot<TBucketId, TStreamId>> _snapshots;
        private volatile int _isDisposed = 0;

        public MongoStreamPersistence(IMongoDatabase database, ISnapshotProcessor<TBucketId, TStreamId> snapshotProcessor)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (snapshotProcessor == null)
                throw new ArgumentNullException(nameof(snapshotProcessor));

            _database = database;
            _snapshotProcessor = snapshotProcessor;
            _streamHeads = database.GetCollection<MongoStreamHead<TBucketId, TStreamId>>("stream-store.stream-heads");
            _commits = database.GetCollection<MongoCommit<TBucketId, TStreamId>>("stream-store.commits");
            _snapshots = database.GetCollection<MongoSnapshot<TBucketId, TStreamId>>("stream-store.snapshots");
        }

        #region Disposal

        public bool IsDisposed => _isDisposed == 1;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            // TODO: Dispose
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        #endregion

        public async Task<bool> AddSnapshotAsync(ISnapshot<TBucketId, TStreamId> snapshot, CancellationToken cancellation)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var wrappedSnapshot = new MongoSnapshot<TBucketId, TStreamId>(snapshot);

            await TryWriteOperation(() => _snapshots.InsertOneAsync(wrappedSnapshot, options: null, cancellationToken: cancellation));

            await UpdateStreamHeadSnapshotRevisionAsync(snapshot.BucketId, snapshot.StreamId, snapshot.StreamRevision);

            return true;
        }

        public async Task<ISnapshot<TBucketId, TStreamId>> GetSnapshotAsync(TBucketId bucketId, TStreamId streamId, long maxRevision, CancellationToken cancellation = default)
        {
            if (maxRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRevision));

            if (maxRevision == default)
            {
                return (await _snapshots.AsQueryable()
                                        .Where(s => s.BucketId.Equals(bucketId) && s.StreamId.Equals(streamId))
                                        .ToListAsync(cancellation))
                       .OrderByDescending(p => p.StreamRevision)
                       .FirstOrDefault();
            }
            else
            {
                return (await _snapshots.AsQueryable()
                                        .Where(s => s.BucketId.Equals(bucketId) && s.StreamId.Equals(streamId) && s.StreamRevision <= maxRevision)
                                        .ToListAsync(cancellation))
                       .OrderByDescending(p => p.StreamRevision)
                       .FirstOrDefault();
            }
        }

        public async Task<IEnumerable<ISnapshot<TBucketId, TStreamId>>> GetSnapshotsAsync(TBucketId bucketId, CancellationToken cancellation = default)
        {
            var snapshots = await _snapshots.AsQueryable()
                                            .Where(s => s.BucketId.Equals(bucketId))
                                            .ToListAsync(cancellation);

            return snapshots.GroupBy(p => p.StreamId)
                            .Select(p => p.OrderByDescending(q => q.StreamRevision)
                                          .FirstOrDefault());
        }

        public async Task<IEnumerable<ISnapshot<TBucketId, TStreamId>>> GetSnapshotsAsync(CancellationToken cancellation = default)
        {
            var snapshots = await _snapshots.AsQueryable()
                                            .ToListAsync(cancellation);

            return snapshots.GroupBy(p => p.StreamId)
                            .Select(p => p.OrderByDescending(q => q.StreamRevision)
                                          .FirstOrDefault());
        }

        public IAsyncEnumerable<IStreamHead<TBucketId, TStreamId>> GetStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation = default)
        {
            return new MongoQueryEvaluator<MongoStreamHead<TBucketId, TStreamId>>(_streamHeads,
                                                                                head => head.HeadRevisionAdvance >= maxThreshold,
                                                                                cancellation);
        }

        public IAsyncEnumerable<IStreamHead<TBucketId, TStreamId>> GetStreamsToSnapshotAsync(TBucketId bucketId, long maxThreshold, CancellationToken cancellation)
        {
            return new MongoQueryEvaluator<MongoStreamHead<TBucketId, TStreamId>>(_streamHeads,
                                                                                head => head.BucketId.Equals(bucketId) && head.HeadRevisionAdvance >= maxThreshold && head.HeadRevision > 0,
                                                                                cancellation);
        }

        public IAsyncEnumerable<IStreamHead<TBucketId, TStreamId>> GetStreamHeadsAsync(TBucketId bucketId, CancellationToken cancellation)
        {
            return new MongoQueryEvaluator<MongoStreamHead<TBucketId, TStreamId>>(_streamHeads,
                                                                                head => head.BucketId.Equals(bucketId) && head.HeadRevision > 0,
                                                                                cancellation);
        }

        public IAsyncEnumerable<IStreamHead<TBucketId, TStreamId>> GetStreamHeadsAsync(CancellationToken cancellation)
        {
            return new MongoQueryEvaluator<MongoStreamHead<TBucketId, TStreamId>>(_streamHeads,
                                                                                head => head.HeadRevision > 0,
                                                                                cancellation);
        }

        public async Task<ICommit<TBucketId, TStreamId>> CommitAsync(CommitAttempt<TBucketId, TStreamId> attempt, CancellationToken cancellation)
        {
            if (attempt.StreamRevision == 1)
            {
                await TryWriteOperation(() => AddStreamHeadAsync(attempt.BucketId, attempt.StreamId, headRevision: 0, snapshotRevision: 0, dispatchedRevision: 0));
            }

            var commit = new MongoCommit<TBucketId, TStreamId>(
                attempt.BucketId,
                attempt.StreamId,
                attempt.ConcurrencyToken,
                attempt.StreamRevision,
                attempt.CommitStamp,
                attempt.Headers,
                attempt.Body,
                attempt.Events,
                isDispatched: false);

            try
            {
                await TryWriteOperation(() => _commits.InsertOneAsync(commit, options: null, cancellationToken: default));
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested && attempt.StreamRevision == 1)
            {
                await TryWriteOperation(() => _streamHeads.DeleteOneAsync(head => head.BucketId.Equals(attempt.BucketId) && head.StreamId.Equals(attempt.StreamId)));

                throw;
            }

            await UpdateStreamHeadRevisionAsync(attempt.BucketId, attempt.StreamId, commit.StreamRevision);

            return commit;
        }

        public async Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync(TBucketId bucketId, TStreamId streamId, long minRevision = 0, long maxRevision = 0, CancellationToken cancellation = default)
        {
            if (bucketId == null)
                throw new ArgumentNullException(nameof(bucketId));

            if (streamId == null)
                throw new ArgumentNullException(nameof(streamId));

            return (await GetCommitsInternalAsync(bucketId, streamId, minRevision, maxRevision, cancellation)).Where(commit => !commit.IsDeleted);
        }

        public async Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync(TBucketId bucketId, CancellationToken cancellation = default)
        {
            if (bucketId == null)
                throw new ArgumentNullException(nameof(bucketId));

            return (await _commits.AsQueryable().Where(commit => commit.BucketId.Equals(bucketId) && !commit.IsDeleted).ToListAsync(cancellation));
        }

        public async Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetCommitsAsync(CancellationToken cancellation = default)
        {
            return (await _commits.AsQueryable().Where(commit => !commit.IsDeleted).ToListAsync(cancellation));
        }

        private async Task<IEnumerable<MongoCommit<TBucketId, TStreamId>>> GetCommitsInternalAsync(TBucketId bucketId, TStreamId streamId, long minRevision = 0, long maxRevision = 0, CancellationToken cancellation = default)
        {
            if (minRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(minRevision));

            if (maxRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRevision));

            if (maxRevision == default)
            {
                return (await _commits.AsQueryable()
                                     .Where(commit => commit.BucketId.Equals(bucketId) &&
                                                      commit.StreamId.Equals(streamId) &&
                                                      commit.StreamRevision >= minRevision)
                                     .ToListAsync(cancellation))
                       .OrderBy(commit => commit.StreamRevision);
            }

            return (await _commits.AsQueryable()
                                 .Where(commit => commit.BucketId.Equals(bucketId) &&
                                                  commit.StreamId.Equals(streamId) &&
                                                  commit.StreamRevision >= minRevision &&
                                                  commit.StreamRevision <= maxRevision)
                                 .ToListAsync(cancellation))
                   .OrderBy(commit => commit.StreamRevision);
        }

        public async Task<IEnumerable<ICommit<TBucketId, TStreamId>>> GetUndispatchedCommitsAsync(CancellationToken cancellation)
        {
            var result = new List<MongoCommit<TBucketId, TStreamId>>();
            var heads = await _streamHeads.AsQueryable().ToListAsync(cancellation);

            foreach (var head in heads)
            {
                var commits = (await GetCommitsInternalAsync(head.BucketId, head.StreamId, head.DispatchedRevision, cancellation: cancellation)).Where(commit => !commit.IsDeleted);

                foreach (var commit in commits)
                {
                    if (!commit.IsDispatched)
                    {
                        result.Add(commit);
                    }
                }
            }

            return result;
        }

        public async Task MarkCommitAsDispatchedAsync(ICommit<TBucketId, TStreamId> commit, CancellationToken cancellation)
        {
            await TryWriteOperation(() => _commits.UpdateOneAsync(c => c.BucketId.Equals(commit.BucketId) &&
                                                                       c.StreamId.Equals(commit.StreamId) &&
                                                                       c.StreamRevision == commit.StreamRevision,
                                                                  Builders<MongoCommit<TBucketId, TStreamId>>.Update.Set(c => c.IsDispatched, true),
                                                                  cancellationToken: cancellation));

            await UpdateStreamHeadDispatchedRevisionAsync(commit.BucketId, commit.StreamId, commit.StreamRevision);
        }

        public async Task DeleteStreamAsync(TBucketId bucketId, TStreamId streamId, CancellationToken cancellation)
        {
            await TryWriteOperation(() => _snapshots.DeleteManyAsync(p => p.BucketId.Equals(bucketId) && p.StreamId.Equals(streamId), cancellation));

            // TODO: What to do if the op fails in between?
            await TryWriteOperation(() => _commits.DeleteManyAsync(p => p.BucketId.Equals(bucketId) && p.StreamId.Equals(streamId), cancellation));

            // TODO: How do we get rid of the stream heads?
        }

        #region StreamHead

        private async Task<MongoStreamHead<TBucketId, TStreamId>> AddStreamHeadAsync(TBucketId bucketId, TStreamId streamId)
        {
            var commits = (await GetCommitsInternalAsync(bucketId, streamId)).Where(commit => !commit.IsDeleted);

            if (!commits.Any())
            {
                return null;
            }

            var snapshot = await GetSnapshotAsync(bucketId, streamId, maxRevision: default);
            var dispachedRevision = LatestDispatchedCommitAsync(commits);

            return await AddStreamHeadAsync(bucketId, streamId, commits.Last().StreamRevision, snapshot?.StreamRevision ?? 0, dispachedRevision?.StreamRevision ?? 0);
        }

        private async Task<MongoStreamHead<TBucketId, TStreamId>> AddStreamHeadAsync(TBucketId bucketId, TStreamId streamId, long headRevision, long snapshotRevision, long dispatchedRevision)
        {
            var streamHead = new MongoStreamHead<TBucketId, TStreamId>(bucketId, streamId, headRevision, snapshotRevision, dispatchedRevision);

            await TryWriteOperation(() => _streamHeads.InsertOneAsync(streamHead, options: null, cancellationToken: default));

            return streamHead;
        }

        private async Task UpdateStreamHeadRevisionAsync(TBucketId bucketId, TStreamId streamId, long headRevision)
        {
            var result = default(ReplaceOneResult);

            do
            {
                var streamHead = _streamHeads.AsQueryable()
                                             .Where(head => head.BucketId.Equals(bucketId) && head.StreamId.Equals(streamId))
                                             .FirstOrDefault();

                if (streamHead == null)
                {
                    if (headRevision > 0)
                        streamHead = await AddStreamHeadAsync(bucketId, streamId);

                    return;
                }

                if (headRevision > 0 && streamHead.HeadRevision >= headRevision)
                {
                    return;
                }

                var oldHeadRevision = streamHead.HeadRevision;

                streamHead.HeadRevision = headRevision;
                streamHead.HeadRevisionAdvance = streamHead.HeadRevision - streamHead.SnapshotRevision;

                result = await TryWriteOperation(() => _streamHeads.ReplaceOneAsync(
                                        head => head.BucketId.Equals(bucketId) &&
                                                head.StreamId.Equals(streamId) &&
                                                head.HeadRevision == oldHeadRevision &&
                                                head.SnapshotRevision == streamHead.SnapshotRevision,
                                        streamHead,
                                        options: new UpdateOptions { IsUpsert = false }));

                if (!result.IsAcknowledged || !result.IsModifiedCountAvailable)
                {
                    return; // TODO: What to do?
                }
            }
            while (result.ModifiedCount == 0);
        }

        private async Task UpdateStreamHeadSnapshotRevisionAsync(TBucketId bucketId, TStreamId streamId, long snapshotRevision)
        {
            var result = default(ReplaceOneResult);

            do
            {
                var streamHead = _streamHeads.AsQueryable()
                                              .Where(head => head.BucketId.Equals(bucketId) && head.StreamId.Equals(streamId))
                                              .FirstOrDefault();

                if (streamHead == null)
                {
                    // TODO: Log. This may never happen.

                    streamHead = await AddStreamHeadAsync(bucketId, streamId);
                }

                if (streamHead.SnapshotRevision >= snapshotRevision)
                    return;

                var oldSnapshotRevision = streamHead.SnapshotRevision;

                streamHead.SnapshotRevision = snapshotRevision;
                streamHead.HeadRevisionAdvance = streamHead.HeadRevision - streamHead.SnapshotRevision;

                result = await TryWriteOperation(() => _streamHeads.ReplaceOneAsync(
                                        head => head.BucketId.Equals(bucketId) &&
                                                head.StreamId.Equals(streamId) &&
                                                head.HeadRevision == streamHead.HeadRevision &&
                                                head.SnapshotRevision == oldSnapshotRevision,
                                        streamHead,
                                        options: new UpdateOptions { IsUpsert = false }));

                if (!result.IsAcknowledged || !result.IsModifiedCountAvailable)
                {
                    return; // TODO: What to do?
                }
            }
            while (result.ModifiedCount == 0);
        }

        private async Task UpdateStreamHeadDispatchedRevisionAsync(TBucketId bucketId, TStreamId streamId, long dispatchedRevision)
        {
            var headUpdateResult = await TryWriteOperation(() => _streamHeads.UpdateOneAsync(
                                        head => head.BucketId.Equals(bucketId) &&
                                                head.StreamId.Equals(streamId) &&
                                                head.DispatchedRevision == dispatchedRevision - 1,
                                        Builders<MongoStreamHead<TBucketId, TStreamId>>.Update.Set(head => head.DispatchedRevision, dispatchedRevision)));

            if (!headUpdateResult.IsAcknowledged || !headUpdateResult.IsModifiedCountAvailable)
            {
                return; // TODO: What to do?
            }

            if (headUpdateResult.ModifiedCount == 0)
            {
                var streamHead = await _streamHeads.AsQueryable().Where(head => head.BucketId.Equals(bucketId) && head.StreamId.Equals(streamId)).FirstOrDefaultAsync();

                if (streamHead == null)
                {
                    // TODO: Log. This may never happen.

                    await AddStreamHeadAsync(bucketId, streamId);
                }
                else
                {
                    var commits = (await GetCommitsInternalAsync(bucketId, streamId, streamHead.DispatchedRevision + 1, dispatchedRevision)).Where(commit => !commit.IsDeleted);
                    var latestDispatchedCommit = LatestDispatchedCommitAsync(commits);

                    if (latestDispatchedCommit != null)
                    {
                        dispatchedRevision = latestDispatchedCommit.StreamRevision;

                        await TryWriteOperation(() => _streamHeads.UpdateOneAsync(
                            head => head.BucketId.Equals(bucketId) &&
                                    head.StreamId.Equals(streamId) &&
                                    head.DispatchedRevision <= dispatchedRevision,
                            Builders<MongoStreamHead<TBucketId, TStreamId>>.Update.Set(head => head.DispatchedRevision, dispatchedRevision)));
                    }
                }
            }
        }

        private static MongoCommit<TBucketId, TStreamId> LatestDispatchedCommitAsync(IEnumerable<MongoCommit<TBucketId, TStreamId>> commits)
        {
            var result = default(MongoCommit<TBucketId, TStreamId>);

            foreach (var commit in commits)
            {
                if (!commit.IsDispatched)
                {
                    return result;
                }

                result = commit;
            }

            return result;
        }

        #endregion
    }
}
