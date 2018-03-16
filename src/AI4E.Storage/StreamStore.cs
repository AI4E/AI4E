/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        StreamStore.cs 
 * Types:           (1) AI4E.Storage.StreamStore'2
 *                  (2) AI4E.Storage.StreamStore'2.Stream
 *                  (3) AI4E.Storage.StreamStore'2.Snapshot
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   16.01.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * NEventStore (https://github.com/NEventStore/NEventStore)
 * The MIT License
 * 
 * Copyright (c) 2013 Jonathan Oliver, Jonathan Matheus, Damian Hickey and contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Internal;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage
{
    public sealed partial class StreamStore<TBucketId, TStreamId> : IStreamStore<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        #region Fields

        private readonly ILogger _logger;
        private readonly IStreamPersistence<TBucketId, TStreamId> _persistence;
        private readonly ICommitDispatcher<TBucketId, TStreamId> _commitDispatcher;
        private readonly IEnumerable<IStorageExtension<TBucketId, TStreamId>> _extensions;
        private readonly Dictionary<(TBucketId bucketId, TStreamId streamId, long revision), Stream> _streams;
        private bool _isDisposed;

        #endregion

        #region C'tor

        public StreamStore(IStreamPersistence<TBucketId, TStreamId> persistence,
                          ICommitDispatcher<TBucketId, TStreamId> commitDispatcher,
                          IEnumerable<IStorageExtension<TBucketId, TStreamId>> extensions)
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            if (commitDispatcher == null)
                throw new ArgumentNullException(nameof(commitDispatcher));

            if (extensions == null)
                throw new ArgumentNullException(nameof(extensions));

            _persistence = persistence;
            _commitDispatcher = commitDispatcher;
            _extensions = extensions;
            _streams = new Dictionary<(TBucketId bucketId, TStreamId streamId, long revision), Stream>();
        }

        public StreamStore(IStreamPersistence<TBucketId, TStreamId> persistence,
                          ICommitDispatcher<TBucketId, TStreamId> commitDispatcher,
                          IEnumerable<IStorageExtension<TBucketId, TStreamId>> extensions,
                          ILogger<StreamStore<TBucketId, TStreamId>> logger)
            : this(persistence, commitDispatcher, extensions)
        {
            _logger = logger;
        }

        #endregion

        #region IStreamStore

        public Task<IStream<TBucketId, TStreamId>> OpenStreamAsync(TBucketId bucketId, TStreamId streamId, bool throwIfNotFound, CancellationToken cancellation)
        {
            var result = OpenStreamAsync(bucketId, streamId, revision: default, cancellation);

            if (throwIfNotFound)
            {
                return ValidateStreamExistsAsync(result);
            }

            return result;
        }

        private async Task<IStream<TBucketId, TStreamId>> ValidateStreamExistsAsync(Task<IStream<TBucketId, TStreamId>> result)
        {
            var stream = await result;

            if (stream.StreamRevision == 0)
            {
                throw new StreamNotFoundException();
            }

            return stream;
        }

        public async Task<IStream<TBucketId, TStreamId>> OpenStreamAsync(TBucketId bucketId, TStreamId streamId, long revision, CancellationToken cancellation)
        {
            if (!_streams.TryGetValue((bucketId, streamId, revision), out var stream))
            {
                stream = await Stream.OpenAsync(this, bucketId, streamId, revision, _logger, cancellation);

                _streams.Add((bucketId, streamId, revision), stream);
            }

            return stream;
        }

        public IAsyncEnumerable<IStream<TBucketId, TStreamId>> OpenAllAsync(TBucketId bucketId, CancellationToken cancellation)
        {
            return _persistence.GetStreamHeadsAsync(bucketId, cancellation)
                               .SelectOrContinue(head => OpenStreamAsync(head.BucketId, head.StreamId, throwIfNotFound: false, cancellation));
        }

        public IAsyncEnumerable<IStream<TBucketId, TStreamId>> OpenAllAsync(CancellationToken cancellation)
        {
            return _persistence.GetStreamHeadsAsync(cancellation)
                               .SelectOrContinue(head => OpenStreamAsync(head.BucketId, head.StreamId, throwIfNotFound: false, cancellation));
        }

        public IAsyncEnumerable<IStream<TBucketId, TStreamId>> OpenStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation)
        {
            return _persistence.GetStreamsToSnapshotAsync(maxThreshold, cancellation)
                               .SelectOrContinue(head => OpenStreamAsync(head.BucketId, head.StreamId, throwIfNotFound: false, cancellation));
        }

        #endregion

        #region IDisposal

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _logger?.LogInformation(Resources.ShuttingDownStore);

            foreach (var extension in _extensions)
            {
                extension.Dispose();
            }

            _persistence.Dispose();
        }

        #endregion

        private sealed class Stream : IStream<TBucketId, TStreamId>
        {
            #region Fields

            private ISnapshot<TBucketId, TStreamId> _snapshot;
            private readonly List<ICommit<TBucketId, TStreamId>> _commits = new List<ICommit<TBucketId, TStreamId>>();
            private readonly StreamStore<TBucketId, TStreamId> _streamStore;
            private readonly ILogger _logger;
            private readonly bool _isFixedRevision = false;

            #endregion

            #region C'tor

            private Stream(StreamStore<TBucketId, TStreamId> streamStore,
                               TBucketId bucketId,
                               TStreamId streamId,
                               ISnapshot<TBucketId, TStreamId> snapshot,
                               IEnumerable<ICommit<TBucketId, TStreamId>> commits,
                               bool isFixedRevision,
                               ILogger logger)
            {
                if (streamStore == null)
                    throw new ArgumentNullException(nameof(streamStore));

                if (commits == null)
                    throw new ArgumentNullException(nameof(commits));

                _streamStore = streamStore;
                BucketId = bucketId;
                StreamId = streamId;
                _snapshot = snapshot;
                _logger = logger;
                _commits.AddRange(commits);
                _isFixedRevision = isFixedRevision;
            }

            #endregion

            public static async Task<Stream> OpenAsync(StreamStore<TBucketId, TStreamId> streamStore,
                                                            TBucketId bucketId,
                                                            TStreamId streamId,
                                                            long revision,
                                                            ILogger logger,
                                                            CancellationToken cancellation)
            {
                if (streamStore == null)
                    throw new ArgumentNullException(nameof(streamStore));

                if (revision < 0)
                    throw new ArgumentOutOfRangeException(nameof(revision));

                var snapshot = await streamStore._persistence.GetSnapshotAsync(bucketId, streamId, revision, cancellation);
                var commits = await streamStore._persistence.GetCommitsAsync(bucketId, streamId, (snapshot?.StreamRevision + 1) ?? default, revision, cancellation)
                    ?? Enumerable.Empty<ICommit<TBucketId, TStreamId>>();
                var isFixedRevision = revision != default;

                var result = new Stream(streamStore, bucketId, streamId, snapshot, commits, isFixedRevision, logger);

                if (isFixedRevision && result.StreamRevision != revision)
                {
                    throw new StorageException($"Unable to load stream in revision {revision}.");
                }

                return result;
            }

            public TBucketId BucketId { get; }
            public TStreamId StreamId { get; }

            public ISnapshot<TBucketId, TStreamId> Snapshot => _snapshot;
            public IEnumerable<ICommit<TBucketId, TStreamId>> Commits => _commits.AsReadOnly();

            public long StreamRevision => Commits.LastOrDefault()?.StreamRevision ?? Snapshot?.StreamRevision ?? 0;
            public Guid ConcurrencyToken => Commits.LastOrDefault()?.ConcurrencyToken ?? Snapshot?.ConcurrencyToken ?? Guid.Empty;
            public IReadOnlyDictionary<string, object> Headers => GetHeaders().ToImmutableDictionary();
            public IReadOnlyList<EventMessage> Events => Commits.SelectMany(commit => commit.Events ?? Enumerable.Empty<EventMessage>()).ToImmutableArray();

            public bool IsReadOnly => _isFixedRevision;

            public IDictionary<string, object> GetHeaders()
            {
                var result = new Dictionary<string, object>();
                var commit = Commits.LastOrDefault();
                var source = default(IReadOnlyDictionary<string, object>);

                if (commit != null)
                {
                    source = commit.Headers;
                }
                else if (Snapshot != null)
                {
                    source = Snapshot.Headers;
                }

                if (source != null)
                {
                    foreach (var entry in source)
                    {
                        result.Add(entry.Key, entry.Value);
                    }
                }

                return result;
            }

            public async Task AddSnapshotAsync(object body, CancellationToken cancellation = default)
            {
                if (_isFixedRevision)
                    throw new InvalidOperationException("Cannot modify a read-only stream view.");

                var snapshot = new Snapshot(BucketId, StreamId, StreamRevision, body, Headers, ConcurrencyToken);

                await _streamStore._persistence.AddSnapshotAsync(snapshot, cancellation);

                _snapshot = snapshot;
                _commits.Clear();
            }

            public async Task<Guid> CommitAsync(Guid concurrencyToken, IEnumerable<EventMessage> events, object body, Action<IDictionary<string, object>> headerGenerator, CancellationToken cancellation)
            {
                if (_isFixedRevision)
                    throw new InvalidOperationException("Cannot modify a read-only stream view.");

                _logger?.LogDebug(Resources.AttemptingToCommitChanges, StreamId);
                await CheckConcurrencyAsync(concurrencyToken, cancellation);

                Guid newToken;

                for (var i = 0; i < 10; i++)
                {
                    newToken = Guid.NewGuid();

                    if (Commits.Any(p => p.ConcurrencyToken == newToken))
                    {
                        continue;
                    }

                    try
                    {
                        var headers = GetHeaders();
                        headerGenerator(headers);

                        var commit = await PersistChangesAsync(newToken,
                                                               events,
                                                               body,
                                                               (headers as IReadOnlyDictionary<string, object>) ?? headers.ToImmutableDictionary(),
                                                               cancellation);

                        // Commit rejected by extension
                        if (commit == null)
                        {
                            return Guid.Empty;
                        }

                        await _streamStore._commitDispatcher.DispatchAsync(commit);

                        return newToken;
                    }
                    catch (DuplicateCommitException)
                    {
                        continue;
                    }
                    catch (ConcurrencyException)
                    {
                        await UpdateAsync(cancellation);

                        throw;
                    }
                }
                throw new StorageException("Gived up on unique concurrency token generation.");
            }

            public async Task<bool> UpdateAsync(CancellationToken cancellation)
            {
                if (_isFixedRevision)
                    throw new InvalidOperationException("Cannot modify a read-only stream view.");

                var commits = await _streamStore._persistence.GetCommitsAsync(BucketId, StreamId, StreamRevision + 1, cancellation);

                if (commits.Any())
                {
                    _logger?.LogInformation(Resources.UnderlyingStreamHasChanged, StreamId);
                    _commits.AddRange(commits);
                    return true;
                }

                return false;
            }

            private async Task CheckConcurrencyAsync(Guid concurrencyToken, CancellationToken cancellation)
            {
                if (concurrencyToken == ConcurrencyToken)
                    return;

                if (Commits.TakeAllButLast().Any(p => p.ConcurrencyToken == concurrencyToken))
                {
                    throw new ConcurrencyException();
                }

                if (await UpdateAsync(cancellation) && concurrencyToken == ConcurrencyToken)
                {
                    return;
                }

                // Either a concurrency token of a commit that is not present because of a snapshot or an unknown token 
                throw new ConcurrencyException();
            }

            private IEnumerable<ICommit<TBucketId, TStreamId>> ExecuteExtensions(IEnumerable<ICommit<TBucketId, TStreamId>> commits)
            {
                foreach (var commit in commits)
                {
                    var filtered = commit;
                    foreach (var extension in _streamStore._extensions.Where(x => (filtered = x.Select(filtered)) == null))
                    {
                        _logger?.LogInformation(Resources.PipelineHookSkippedCommit, extension.GetType(), commit.ConcurrencyToken);
                        break;
                    }

                    if (filtered == null)
                    {
                        _logger?.LogInformation(Resources.PipelineHookFilteredCommit);
                    }
                    else
                    {
                        yield return filtered;
                    }
                }
            }

            private async Task<ICommit<TBucketId, TStreamId>> PersistChangesAsync(Guid newConcurrencyToken,
                                                                                IEnumerable<EventMessage> events,
                                                                                object body,
                                                                                IReadOnlyDictionary<string, object> headers,
                                                                                CancellationToken cancellation)
            {
                var attempt = BuildCommitAttempt(newConcurrencyToken, events, body, headers);

                foreach (var extension in _streamStore._extensions)
                {
                    _logger?.LogDebug(Resources.InvokingPreCommitHooks, attempt.ConcurrencyToken, extension.GetType());
                    if (extension.PreCommit(attempt))
                    {
                        continue;
                    }

                    _logger?.LogInformation(Resources.CommitRejectedByPipelineHook, extension.GetType(), attempt.ConcurrencyToken);
                    return null;
                }

                _logger?.LogDebug(Resources.PersistingCommit, newConcurrencyToken, StreamId);
                var commit = await _streamStore._persistence.CommitAsync(attempt, cancellation);

                try
                {
                    foreach (var extension in _streamStore._extensions)
                    {
                        _logger?.LogDebug(Resources.InvokingPostCommitPipelineHooks, attempt.ConcurrencyToken, extension.GetType());
                        extension.PostCommit(commit);
                    }
                }
                finally
                {
                    _commits.Add(commit);
                }

                return commit;
            }

            private CommitAttempt<TBucketId, TStreamId> BuildCommitAttempt(Guid newConcurrencyToken,
                                                                         IEnumerable<EventMessage> events,
                                                                         object body,
                                                                         IReadOnlyDictionary<string, object> headers)
            {
                _logger?.LogDebug(Resources.BuildingCommitAttempt, newConcurrencyToken, StreamId);
                return new CommitAttempt<TBucketId, TStreamId>(
                    BucketId,
                    StreamId,
                    newConcurrencyToken,
                    StreamRevision + 1,
                    SystemTime.UtcNow,
                    headers.ToDictionary(x => x.Key, x => x.Value),
                    body,
                    events.ToList());
            }
        }

        private sealed class Snapshot : ISnapshot<TBucketId, TStreamId>
        {
            public Snapshot(TBucketId bucketId, TStreamId streamId, long streamRevision, object payload, IReadOnlyDictionary<string, object> headers, Guid concurrencyToken)
            {
                BucketId = bucketId;
                StreamId = streamId;
                Payload = payload;
                Headers = headers;
                StreamRevision = streamRevision;
                ConcurrencyToken = concurrencyToken;
            }

            public TBucketId BucketId { get; }

            public TStreamId StreamId { get; }

            public object Payload { get; }

            public IReadOnlyDictionary<string, object> Headers { get; }

            public long StreamRevision { get; }

            public Guid ConcurrencyToken { get; }
        }
    }
}
