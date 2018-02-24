/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EventStore.cs 
 * Types:           (1) AI4E.Storage.EventStore'2
 *                  (2) AI4E.Storage.EventStore'2.EventStream
 *                  (3) AI4E.Storage.EventStore'2.Snapshot
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
using Microsoft.Extensions.Logging;

namespace AI4E.Storage
{
    public sealed partial class EventStore<TBucket, TStreamId> : IEventStore<TBucket, TStreamId> // TODO: Rename to StreamStore
        where TBucket : IEquatable<TBucket>
        where TStreamId : IEquatable<TStreamId>
    {
        #region Fields

        private readonly ILogger _logger;
        private readonly IStreamPersistence<TBucket, TStreamId> _persistence;
        private readonly ICommitDispatcher<TBucket, TStreamId> _commitDispatcher;
        private readonly IEnumerable<IStorageExtension<TBucket, TStreamId>> _extensions;
        private readonly Dictionary<(TBucket bucketId, TStreamId streamId), EventStream> _streams;
        private bool _isDisposed;

        #endregion

        #region C'tor

        public EventStore(IStreamPersistence<TBucket, TStreamId> persistence,
                          ICommitDispatcher<TBucket, TStreamId> commitDispatcher,
                          IEnumerable<IStorageExtension<TBucket, TStreamId>> extensions)
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
            _streams = new Dictionary<(TBucket bucketId, TStreamId streamId), EventStream>();
        }

        public EventStore(IStreamPersistence<TBucket, TStreamId> persistence,
                          ICommitDispatcher<TBucket, TStreamId> commitDispatcher,
                          IEnumerable<IStorageExtension<TBucket, TStreamId>> extensions,
                          ILogger<EventStore<TBucket, TStreamId>> logger)
            : this(persistence, commitDispatcher, extensions)
        {
            _logger = logger;
        }

        #endregion

        #region IEventStore

        public async Task<IEventStream<TBucket, TStreamId>> OpenStreamAsync(TBucket bucketId, TStreamId streamId, CancellationToken cancellation)
        {
            if (!_streams.TryGetValue((bucketId, streamId), out var stream))
            {
                stream = await EventStream.OpenAsync(this, bucketId, streamId, _logger, cancellation);

                _streams.Add((bucketId, streamId), stream);
            }

            return stream;
        }

        public async Task<IEnumerable<IEventStream<TBucket, TStreamId>>> OpenAllAsync(TBucket bucketId, CancellationToken cancellation)
        {
            return await Task.WhenAll((await _persistence.GetStreamHeadsAsync(bucketId, cancellation)).Select(head => OpenStreamAsync(head.BucketId, head.StreamId, cancellation)));
        }

        public async Task<IEnumerable<IEventStream<TBucket, TStreamId>>> OpenAllAsync(CancellationToken cancellation)
        {
            return await Task.WhenAll((await _persistence.GetStreamHeadsAsync(cancellation)).Select(head => OpenStreamAsync(head.BucketId, head.StreamId, cancellation)));
        }

        public async Task<IEnumerable<IEventStream<TBucket, TStreamId>>> OpenStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation)
        {
            var heads = await _persistence.GetStreamsToSnapshotAsync(maxThreshold, cancellation);

            return await Task.WhenAll(heads.Select(head => OpenStreamAsync(head.BucketId, head.StreamId, cancellation)));
        }

        //public Task AddSnapshotAsync(ISnapshot<TBucket, TStreamId> snapshot, CancellationToken cancellation)
        //{
        //    return _persistence.AddSnapshotAsync(snapshot, cancellation);
        //}

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

        private sealed class EventStream : IEventStream<TBucket, TStreamId>
        {
            #region Fields

            private ISnapshot<TBucket, TStreamId> _snapshot;
            private readonly List<ICommit<TBucket, TStreamId>> _commits = new List<ICommit<TBucket, TStreamId>>();
            private readonly EventStore<TBucket, TStreamId> _eventStore;
            private readonly ILogger _logger;

            #endregion

            #region C'tor

            private EventStream(EventStore<TBucket, TStreamId> eventStore,
                               TBucket bucketId,
                               TStreamId streamId,
                               ISnapshot<TBucket, TStreamId> snapshot,
                               IEnumerable<ICommit<TBucket, TStreamId>> commits,
                               ILogger logger)
            {
                if (eventStore == null)
                    throw new ArgumentNullException(nameof(eventStore));

                if (commits == null)
                    throw new ArgumentNullException(nameof(commits));

                _eventStore = eventStore;
                BucketId = bucketId;
                StreamId = streamId;
                _snapshot = snapshot;
                _logger = logger;
                _commits.AddRange(commits);
            }

            #endregion

            public static async Task<EventStream> OpenAsync(EventStore<TBucket, TStreamId> eventStore,
                                                                  TBucket bucketId,
                                                                  TStreamId streamId,
                                                                  ILogger logger,
                                                                  CancellationToken cancellation)
            {
                if (eventStore == null)
                    throw new ArgumentNullException(nameof(eventStore));

                var snapshot = await eventStore._persistence.GetSnapshotAsync(bucketId, streamId, cancellation);
                var commits = await eventStore._persistence.GetCommitsAsync(bucketId, streamId, (snapshot?.StreamRevision + 1) ?? 0, cancellation)
                    ?? Enumerable.Empty<ICommit<TBucket, TStreamId>>();

                return new EventStream(eventStore, bucketId, streamId, snapshot, commits, logger);
            }

            public TBucket BucketId { get; }
            public TStreamId StreamId { get; }

            public ISnapshot<TBucket, TStreamId> Snapshot => _snapshot;
            public IEnumerable<ICommit<TBucket, TStreamId>> Commits => _commits.AsReadOnly();

            public long StreamRevision => Commits.LastOrDefault()?.StreamRevision ?? Snapshot?.StreamRevision ?? 0;
            public Guid ConcurrencyToken => Commits.LastOrDefault()?.ConcurrencyToken ?? Snapshot?.ConcurrencyToken ?? Guid.Empty;
            public IReadOnlyDictionary<string, object> Headers => GetHeaders().ToImmutableDictionary();
            public IReadOnlyList<EventMessage> Events => Commits.SelectMany(commit => commit.Events ?? Enumerable.Empty<EventMessage>()).ToImmutableArray();

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
                var snapshot = new Snapshot(BucketId, StreamId, StreamRevision, body, Headers, ConcurrencyToken);

                await _eventStore._persistence.AddSnapshotAsync(snapshot, cancellation);

                _snapshot = snapshot;
                _commits.Clear();
            }

            public async Task<Guid> CommitAsync(Guid concurrencyToken, IEnumerable<EventMessage> events, object body, Action<IDictionary<string, object>> headerGenerator, CancellationToken cancellation)
            {
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

                        await _eventStore._commitDispatcher.DispatchAsync(commit);

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
                var commits = await _eventStore._persistence.GetCommitsAsync(BucketId, StreamId, StreamRevision + 1, cancellation);

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

            private IEnumerable<ICommit<TBucket, TStreamId>> ExecuteExtensions(IEnumerable<ICommit<TBucket, TStreamId>> commits)
            {
                foreach (var commit in commits)
                {
                    var filtered = commit;
                    foreach (var extension in _eventStore._extensions.Where(x => (filtered = x.Select(filtered)) == null))
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

            private async Task<ICommit<TBucket, TStreamId>> PersistChangesAsync(Guid newConcurrencyToken,
                                                                                IEnumerable<EventMessage> events,
                                                                                object body,
                                                                                IReadOnlyDictionary<string, object> headers,
                                                                                CancellationToken cancellation)
            {
                var attempt = BuildCommitAttempt(newConcurrencyToken, events, body, headers);

                foreach (var extension in _eventStore._extensions)
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
                var commit = await _eventStore._persistence.CommitAsync(attempt, cancellation);

                try
                {
                    foreach (var extension in _eventStore._extensions)
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

            private CommitAttempt<TBucket, TStreamId> BuildCommitAttempt(Guid newConcurrencyToken,
                                                                         IEnumerable<EventMessage> events,
                                                                         object body,
                                                                         IReadOnlyDictionary<string, object> headers)
            {
                _logger?.LogDebug(Resources.BuildingCommitAttempt, newConcurrencyToken, StreamId);
                return new CommitAttempt<TBucket, TStreamId>(
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

        private sealed class Snapshot : ISnapshot<TBucket, TStreamId>
        {
            public Snapshot(TBucket bucketId, TStreamId streamId, long streamRevision, object payload, IReadOnlyDictionary<string, object> headers, Guid concurrencyToken)
            {
                BucketId = bucketId;
                StreamId = streamId;
                Payload = payload;
                Headers = headers;
                StreamRevision = streamRevision;
                ConcurrencyToken = concurrencyToken;
            }

            public TBucket BucketId { get; }


            public TStreamId StreamId { get; }

            public object Payload { get; }

            public IReadOnlyDictionary<string, object> Headers { get; }

            public long StreamRevision { get; }

            public Guid ConcurrencyToken { get; }
        }
    }
}
