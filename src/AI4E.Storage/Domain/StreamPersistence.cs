using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;

namespace AI4E.Storage.Domain
{
    public sealed class StreamPersistence : IStreamPersistence
    {
        private readonly IFilterableDatabase _database;
        //private readonly ISnapshotProcessor _snapshotProcessor;
        private int _isDisposed;

        public StreamPersistence(IFilterableDatabase database/*, ISnapshotProcessor snapshotProcessor*/)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            //if (snapshotProcessor == null)
            //    throw new ArgumentNullException(nameof(snapshotProcessor));

            _database = database;
            //_snapshotProcessor = snapshotProcessor;
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

        public async Task<bool> AddSnapshotAsync(ISnapshot snapshot, CancellationToken cancellation)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var wrappedSnapshot = new Snapshot(snapshot);

            if (await _database.AddAsync(wrappedSnapshot, cancellation))
            {
                var streamHead = await LoadStreamHeadAsync(snapshot.BucketId, snapshot.StreamId, cancellation);
                await UpdateStreamHeadSnapshotRevisionAsync(streamHead, snapshot.StreamRevision, cancellation);

                return true;
            }

            return false;
        }

        private async Task<StreamHead> LoadStreamHeadAsync(string bucketId, string streamId, CancellationToken cancellation)
        {
            return await _database.GetOneAsync<StreamHead>(head => head.BucketId.Equals(bucketId) && head.StreamId.Equals(streamId), cancellation);
        }

        public Task<ISnapshot> GetSnapshotAsync(string bucketId, string streamId, long maxRevision = default, CancellationToken cancellation = default)
        {
            if (maxRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRevision));

            Expression<Func<Snapshot, bool>> predicate;

            if (maxRevision == default)
            {
                predicate = snapshot => snapshot.BucketId.Equals(bucketId) &&
                                        snapshot.StreamId.Equals(streamId);
            }
            else
            {
                predicate = snapshot => snapshot.BucketId.Equals(bucketId) &&
                                        snapshot.StreamId.Equals(streamId) &&
                                        snapshot.StreamRevision <= maxRevision;
            }

            return _database.GetAsync(predicate, cancellation)
                            .OrderByDescending(snapshot => snapshot.StreamRevision)
                            .Cast<ISnapshot>()
                            .FirstOrDefault(cancellation);
        }

        public IAsyncEnumerable<ISnapshot> GetSnapshotsAsync(string bucketId, CancellationToken cancellation = default)
        {
            // TODO: Check whether the database has query support. 

            return _database.GetAsync<Snapshot>(snapshot => snapshot.BucketId.Equals(bucketId), cancellation)
                            .GroupBy(snapshot => snapshot.StreamId)
                            .Select(group => group.OrderByDescending(snapshot => snapshot.StreamRevision).FirstOrDefault(cancellation))
                            .Evaluate();
        }

        public IAsyncEnumerable<ISnapshot> GetSnapshotsAsync(CancellationToken cancellation = default)
        {
            // TODO: Check whether the database has query support. 

            return _database.GetAsync<Snapshot>(cancellation)
                            .GroupBy(snapshot => snapshot.StreamId)
                            .Select(group => group.OrderByDescending(snapshot => snapshot.StreamRevision).FirstOrDefault(cancellation))
                            .Evaluate();
        }

        public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation = default)
        {
            return _database.GetAsync<StreamHead>(streamHead => streamHead.HeadRevisionAdvance >= maxThreshold &&
                                                                streamHead.HeadRevision > 0, cancellation);
        }

        public IAsyncEnumerable<IStreamHead> GetStreamsToSnapshotAsync(string bucketId,
                                                                       long maxThreshold,
                                                                       CancellationToken cancellation)
        {
            return _database.GetAsync<StreamHead>(streamHead => streamHead.BucketId.Equals(bucketId) &&
                                                                streamHead.HeadRevisionAdvance >= maxThreshold &&
                                                                streamHead.HeadRevision > 0, cancellation);
        }

        public IAsyncEnumerable<IStreamHead> GetStreamHeadsAsync(string bucketId, CancellationToken cancellation)
        {
            return _database.GetAsync<StreamHead>(streamHead => streamHead.BucketId.Equals(bucketId) &&
                                                                streamHead.HeadRevision > 0, cancellation);
        }

        public IAsyncEnumerable<IStreamHead> GetStreamHeadsAsync(CancellationToken cancellation)
        {
            return _database.GetAsync<StreamHead>(streamHead => streamHead.HeadRevision > 0, cancellation);
        }

        public async Task<ICommit> CommitAsync(CommitAttempt attempt, CancellationToken cancellation)
        {
            StreamHead streamHead;

            if (attempt.StreamRevision == 1)
            {
                streamHead = await AddStreamHeadAsync(attempt.BucketId,
                                                      attempt.StreamId,
                                                      headRevision: 0,
                                                      snapshotRevision: 0,
                                                      dispatchedRevision: 0,
                                                      cancellation);
            }
            else
            {
                streamHead = await _database.GetOneAsync<StreamHead>(p => p.BucketId.Equals(attempt.BucketId) &&
                                                                          p.StreamId.Equals(attempt.StreamId), cancellation);
            }

            if (streamHead.HeadRevision >= attempt.StreamRevision)
            {
                throw new ConcurrencyException();
            }

            var commit = BuildCommit(attempt);

            try
            {
                if (!await _database.AddAsync(commit, cancellation))
                {
                    throw new ConcurrencyException();
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested && attempt.StreamRevision == 1)
            {
                // TODO: Are we allowed to delete the stream head here? Another one may add the first commit to the stream concurrently.
                //await TryWriteOperation(() => _streamHeads.DeleteOneAsync(head => head.BucketId.Equals(attempt.BucketId) && head.StreamId.Equals(attempt.StreamId)));

                throw;
            }

            await UpdateStreamHeadRevisionAsync(streamHead, commit.StreamRevision, cancellation);

            return commit;
        }

        private static Commit BuildCommit(CommitAttempt attempt)
        {
            return new Commit(attempt.BucketId,
                              attempt.StreamId,
                              attempt.ConcurrencyToken,
                              attempt.StreamRevision,
                              attempt.CommitStamp,
                              attempt.Headers,
                              attempt.Body,
                              attempt.Events,
                              isDispatched: false);
        }

        public IAsyncEnumerable<ICommit> GetCommitsAsync(string bucketId, string streamId, long minRevision = default, long maxRevision = default, CancellationToken cancellation = default)
        {
            if (bucketId == null)
                throw new ArgumentNullException(nameof(bucketId));

            if (streamId == null)
                throw new ArgumentNullException(nameof(streamId));

            return GetCommitsInternalAsync(bucketId, streamId, minRevision, maxRevision, cancellation).Where(commit => !commit.IsDeleted);
        }

        public IAsyncEnumerable<ICommit> GetCommitsAsync(string bucketId, CancellationToken cancellation = default)
        {
            if (bucketId == null)
                throw new ArgumentNullException(nameof(bucketId));

            return _database.GetAsync<Commit>(commit => commit.BucketId.Equals(bucketId) && !commit.IsDeleted);
        }

        public IAsyncEnumerable<ICommit> GetCommitsAsync(CancellationToken cancellation = default)
        {
            return _database.GetAsync<Commit>(commit => !commit.IsDeleted);
        }

        private IAsyncEnumerable<Commit> GetCommitsInternalAsync(string bucketId,
                                                                 string streamId,
                                                                 long minRevision = 0,
                                                                 long maxRevision = 0,
                                                                 CancellationToken cancellation = default)
        {
            if (minRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(minRevision));

            if (maxRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRevision));

            Expression<Func<Commit, bool>> predicate;

            if (maxRevision == default)
            {
                predicate = commit => commit.BucketId.Equals(bucketId) &&
                                      commit.StreamId.Equals(streamId) &&
                                      commit.StreamRevision >= minRevision;
            }
            else
            {
                predicate = commit => commit.BucketId.Equals(bucketId) &&
                                      commit.StreamId.Equals(streamId) &&
                                      commit.StreamRevision >= minRevision &&
                                      commit.StreamRevision <= maxRevision;
            }

            // TODO: Check whether the database has query support. 
            return _database.GetAsync(predicate, cancellation).OrderBy(commit => commit.StreamRevision);
        }

        public IAsyncEnumerable<ICommit> GetUndispatchedCommitsAsync(CancellationToken cancellation)
        {
            IAsyncEnumerable<Commit> LoadAsync(StreamHead streamHead)
            {
                return GetCommitsInternalAsync(streamHead.BucketId,
                                               streamHead.StreamId,
                                               streamHead.DispatchedRevision, cancellation: cancellation).Where(commit => !commit.IsDeleted);
            }

            return _database.GetAsync<StreamHead>(p => !p.IsDeleted, cancellation)
                            .SelectMany(LoadAsync);
        }

        public async Task MarkCommitAsDispatchedAsync(ICommit commit, CancellationToken cancellation)
        {
            var c = new Commit(commit.BucketId,
                               commit.StreamId,
                               commit.ConcurrencyToken,
                               commit.StreamRevision,
                               commit.CommitStamp,
                               commit.Headers,
                               commit.Body,
                               commit.Events,
                               isDispatched: true);

            await _database.UpdateAsync(c, cancellation);
            var streamHead = await LoadStreamHeadAsync(commit.BucketId, commit.StreamId, cancellation);
            await UpdateStreamHeadDispatchedRevisionAsync(streamHead, commit.StreamRevision, cancellation);
        }

        public async Task DeleteStreamAsync(string bucketId, string streamId, CancellationToken cancellation)
        {
            //await TryWriteOperation(() => _snapshots.DeleteManyAsync(p => p.BucketId.Equals(bucketId) && p.StreamId.Equals(streamId), cancellation));

            // TODO: What to do if the op fails in between?
            // await TryWriteOperation(() => _commits.DeleteManyAsync(p => p.BucketId.Equals(bucketId) && p.StreamId.Equals(streamId), cancellation));

            // TODO: How do we get rid of the stream heads?

            throw new NotImplementedException();
        }

        #region StreamHead

        private async ValueTask<StreamHead> AddStreamHeadAsync(string bucketId, string streamId, CancellationToken cancellation)
        {
            var commits = GetCommitsInternalAsync(bucketId, streamId);
            var latestCommit = await commits.LastOrDefault();

            if (latestCommit == null)
                return null;

            var snapshot = await GetSnapshotAsync(bucketId, streamId, maxRevision: default);
            var dispachedRevision = await LatestDispatchedCommitAsync(commits, cancellation);
            var streamRevision = latestCommit.StreamRevision;

            return await AddStreamHeadAsync(bucketId,
                                            streamId,
                                            streamRevision,
                                            snapshot?.StreamRevision ?? 0,
                                            dispachedRevision?.StreamRevision ?? 0,
                                            cancellation);
        }

        private ValueTask<StreamHead> AddStreamHeadAsync(string bucketId,
                                                         string streamId,
                                                         long headRevision,
                                                         long snapshotRevision,
                                                         long dispatchedRevision,
                                                         CancellationToken cancellation)
        {
            var streamHead = new StreamHead(bucketId, streamId, headRevision, snapshotRevision, dispatchedRevision, version: 1);

            return _database.GetOrAdd(streamHead, cancellation);
        }

        private async ValueTask<StreamHead> UpdateStreamHeadRevisionAsync(StreamHead streamHead, long headRevision, CancellationToken cancellation)
        {
            var id = streamHead.Id;
            var bucketId = streamHead.BucketId;
            var streamId = streamHead.StreamId;

            StreamHead desired;
            while (streamHead.HeadRevision < headRevision)
            {
                desired = new StreamHead(bucketId,
                                         streamId,
                                         headRevision,
                                         streamHead.SnapshotRevision,
                                         streamHead.DispatchedRevision,
                                         streamHead.Version + 1);

                if (await _database.CompareExchangeAsync(desired, streamHead, (left, right) => left.Version == right.Version, cancellation))
                {
                    return desired;
                }

                streamHead = await _database.GetOneAsync<StreamHead>(p => p.Id == id, cancellation);

                if (streamHead == null && (streamHead = await AddStreamHeadAsync(bucketId, streamId, cancellation)) == null)
                {
                    return null;
                }
            }

            return streamHead;
        }

        private async ValueTask<StreamHead> UpdateStreamHeadSnapshotRevisionAsync(StreamHead streamHead, long snapshotRevision, CancellationToken cancellation)
        {
            var id = streamHead.Id;
            var bucketId = streamHead.BucketId;
            var streamId = streamHead.StreamId;

            StreamHead desired;
            while (streamHead.SnapshotRevision < snapshotRevision)
            {
                desired = new StreamHead(bucketId,
                                         streamId,
                                         streamHead.HeadRevision,
                                         snapshotRevision,
                                         streamHead.DispatchedRevision,
                                         streamHead.Version + 1);

                if (await _database.CompareExchangeAsync(desired, streamHead, (left, right) => left.Version == right.Version, cancellation))
                {
                    return desired;
                }

                streamHead = await _database.GetOneAsync<StreamHead>(p => p.Id == id, cancellation);

                if (streamHead == null && (streamHead = await AddStreamHeadAsync(bucketId, streamId, cancellation)) == null)
                {
                    return null;
                }
            }

            return streamHead;
        }

        private async ValueTask<StreamHead> UpdateStreamHeadDispatchedRevisionAsync(StreamHead streamHead, long dispatchedRevision, CancellationToken cancellation)
        {
            var id = streamHead.Id;
            var bucketId = streamHead.BucketId;
            var streamId = streamHead.StreamId;

            StreamHead desired;
            while (streamHead.DispatchedRevision < dispatchedRevision)
            {
                var sequentialDispatchedRevision = dispatchedRevision;

                if (streamHead.DispatchedRevision < dispatchedRevision - 1)
                {
                    var commits = GetCommitsInternalAsync(bucketId, streamId, streamHead.DispatchedRevision + 1, dispatchedRevision);
                    var latestDispatchedCommit = await LatestDispatchedCommitAsync(commits, cancellation);

                    if (latestDispatchedCommit == null)
                        return streamHead;

                    sequentialDispatchedRevision = latestDispatchedCommit.StreamRevision;
                }

                desired = new StreamHead(bucketId,
                                         streamId,
                                         streamHead.HeadRevision,
                                         streamHead.SnapshotRevision,
                                         sequentialDispatchedRevision,
                                         streamHead.Version + 1);

                if (await _database.CompareExchangeAsync(desired, streamHead, (left, right) => left.Version == right.Version, cancellation))
                {
                    return desired;
                }

                streamHead = await _database.GetOneAsync<StreamHead>(p => p.Id == id, cancellation);

                if (streamHead == null && (streamHead = await AddStreamHeadAsync(bucketId, streamId, cancellation)) == null)
                {
                    return null;
                }
            }

            return streamHead;
        }

        private static async ValueTask<Commit> LatestDispatchedCommitAsync(IAsyncEnumerable<Commit> commits, CancellationToken cancellation)
        {
            var result = default(Commit);

            using (var enumerator = commits.GetEnumerator())
            {
                while (await enumerator.MoveNext(cancellation))
                {
                    var commit = enumerator.Current;

                    if (!commit.IsDispatched)
                    {
                        return result;
                    }

                    result = commit;
                }
            }

            return result;
        }

        #endregion

        private sealed class StreamHead : IStreamHead
        {
            private string _id;

            public StreamHead(string bucketId,
                              string streamId,
                              long headRevision,
                              long snapshotRevision,
                              long dispatchedRevision,
                              long version)
            {
                BucketId = bucketId;
                StreamId = streamId;
                HeadRevision = headRevision;
                DispatchedRevision = dispatchedRevision;
                SnapshotRevision = snapshotRevision;
                HeadRevisionAdvance = headRevision - snapshotRevision;
                IsDeleted = false;
                Version = version;
            }

            private StreamHead() { }

            public string Id
            {
                get
                {
                    if (_id == null)
                    {
                        _id = IdGenerator.GenerateId(BucketId, StreamId);
                    }

                    return _id;
                }
                private set => _id = value;
            }

            public long Version { get; private set; }

            public string BucketId { get; private set; }

            public string StreamId { get; private set; }

            public long HeadRevision { get; set; }

            public long SnapshotRevision { get; set; }

            public long DispatchedRevision { get; set; }

            public long HeadRevisionAdvance { get; set; }

            public bool IsDeleted { get; private set; }
        }

        private sealed class Commit : ICommit
        {
            private static readonly IReadOnlyDictionary<string, object> _emptyHeaders = ImmutableDictionary<string, object>.Empty;
            private static readonly IReadOnlyCollection<EventMessage> _emptyEvents = ImmutableList<EventMessage>.Empty;

            private string _id;

            public Commit(string bucketId,
                          string streamId,
                          string concurrencyToken,
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

            private Commit() { }

            public string Id
            {
                get
                {
                    if (_id == null)
                    {
                        _id = IdGenerator.GenerateId(BucketId, StreamId, StreamRevision);
                    }

                    return _id;
                }
                private set => _id = value;
            }

            public string BucketId { get; private set; }

            public string StreamId { get; private set; }

            public string ConcurrencyToken { get; private set; }

            public long StreamRevision { get; private set; }

            public DateTime CommitStamp { get; private set; }

            IReadOnlyDictionary<string, object> ICommit.Headers => Headers ?? _emptyHeaders;

            public Dictionary<string, object> Headers { get; } = new Dictionary<string, object>();

            public object Body { get; private set; }

            IReadOnlyCollection<EventMessage> ICommit.Events => Events ?? _emptyEvents;

            public List<EventMessage> Events { get; } = new List<EventMessage>();

            public bool IsDispatched { get; set; }

            public bool IsDeleted { get; set; }
        }

        private sealed class Snapshot : ISnapshot
        {
            private static readonly IReadOnlyDictionary<string, object> _emptyHeaders = ImmutableDictionary<string, object>.Empty;

            private string _id;

            public Snapshot(string bucketId,
                            string streamId,
                            long streamRevision,
                            object payload,
                            string concurrencyToken,
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

            public Snapshot(ISnapshot snapshot)
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

            private Snapshot() { }

            public string Id
            {
                get
                {
                    if (_id == null)
                    {
                        _id = IdGenerator.GenerateId(BucketId, StreamId, StreamRevision);
                    }

                    return _id;
                }
                private set => _id = value;
            }

            public string BucketId { get; private set; }

            public string StreamId { get; private set; }

            public long StreamRevision { get; private set; }

            public object Payload { get; private set; }

            public string ConcurrencyToken { get; private set; }

            IReadOnlyDictionary<string, object> ISnapshot.Headers => Headers ?? _emptyHeaders;

            Dictionary<string, object> Headers { get; } = new Dictionary<string, object>();
        }
    }
}
