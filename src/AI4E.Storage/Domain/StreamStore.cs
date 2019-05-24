/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        StreamStore.cs 
 * Types:           (1) AI4E.Storage.Domain.StreamStore
 *                  (2) AI4E.Storage.Domain.StreamStore.Stream
 *                  (3) AI4E.Storage.Domain.StreamStore.Snapshot
 * Version:         1.0
 * Author:          Andreas Trütschel
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Microsoft.Extensions.Logging;

namespace AI4E.Storage.Domain
{
    public sealed partial class StreamStore : IStreamStore
    {
        #region Fields

        private readonly ILogger _logger;
        private readonly IStreamPersistence _persistence;
        private readonly ICommitDispatcher _commitDispatcher;
        private readonly ImmutableArray<IStorageExtension> _extensions;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly Dictionary<(string bucketId, string streamId, long revision), StreamData> _streams;
        private readonly object _streamsMutex = new object();
        private bool _isDisposed;

        #endregion

        #region C'tor

        public StreamStore(IStreamPersistence persistence,
                           ICommitDispatcher commitDispatcher,
                           IEnumerable<IStorageExtension> extensions,
                           IDateTimeProvider dateTimeProvider,
                           ILogger<StreamStore> logger = null)
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            if (commitDispatcher == null)
                throw new ArgumentNullException(nameof(commitDispatcher));

            if (extensions == null)
                throw new ArgumentNullException(nameof(extensions));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _persistence = persistence;
            _commitDispatcher = commitDispatcher;
            _extensions = extensions.ToImmutableArray();
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _streams = new Dictionary<(string bucketId, string streamId, long revision), StreamData>();
        }

        #endregion

        #region IStreamStore

        /// <inheritdoc />
        public ValueTask<IStream> OpenStreamAsync(string bucketId, string streamId, bool throwIfNotFound, CancellationToken cancellation)
        {
            var result = OpenStreamAsync(bucketId, streamId, revision: default, cancellation);

            if (throwIfNotFound)
            {
                return ValidateStreamExistsAsync(result);
            }

            return result;
        }

        private async ValueTask<IStream> ValidateStreamExistsAsync(ValueTask<IStream> result)
        {
            var stream = await result;

            if (stream.StreamRevision == 0)
            {
                throw new StreamNotFoundException();
            }

            return stream;
        }

        /// <inheritdoc />   
        public ValueTask<IStream> OpenStreamAsync(string bucketId, string streamId, long revision, CancellationToken cancellation)
        {
            lock (_streamsMutex)
            {
                if (_streams.TryGetValue((bucketId, streamId, revision), out var streamData))
                {
                    Debug.Assert(!(streamData is null));

                    return new ValueTask<IStream>(Stream.FromData(this, streamData, _dateTimeProvider, _logger));
                }
            }

            return new ValueTask<IStream>(OpenStreamUncachedAsync(bucketId, streamId, revision, cancellation));
        }

        private async Task<IStream> OpenStreamUncachedAsync(string bucketId, string streamId, long revision, CancellationToken cancellation)
        {
            Stream stream,
                   desired = await Stream.OpenAsync(this, bucketId, streamId, revision, _dateTimeProvider, _logger, cancellation);

            lock (_streamsMutex)
            {
                if (_streams.TryGetValue((bucketId, streamId, revision), out var streamData))
                {
                    Debug.Assert(!(streamData is null));
                    stream = Stream.FromData(this, streamData, _dateTimeProvider, _logger);
                }
                else
                {
                    _streams[(bucketId, streamId, revision)] = desired.ToData();
                    stream = desired;
                }
            }

            return stream;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IStream> OpenAllAsync(string bucketId, CancellationToken cancellation)
        {
            var streamHeads = _persistence.GetStreamHeadsAsync(bucketId, cancellation);

            await foreach (var streamHead in streamHeads.WithCancellation(cancellation))
            {
                IStream stream;

                try
                {
                    stream = await OpenStreamAsync(streamHead.BucketId, streamHead.StreamId, throwIfNotFound: false, cancellation);
                }
                catch
                {
                    continue; // TODO: Do we really want to silently ignore the exception?
                }

                yield return stream;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IStream> OpenAllAsync(CancellationToken cancellation)
        {
            var streamHeads = _persistence.GetStreamHeadsAsync(cancellation);

            await foreach (var streamHead in streamHeads.WithCancellation(cancellation))
            {
                IStream stream;

                try
                {
                    stream = await OpenStreamAsync(streamHead.BucketId, streamHead.StreamId, throwIfNotFound: false, cancellation);
                }
                catch
                {
                    continue; // TODO: Do we really want to silently ignore the exception?
                }

                yield return stream;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IStream> OpenStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation)
        {
            var streamHeads = _persistence.GetStreamsToSnapshotAsync(maxThreshold, cancellation);

            await foreach (var streamHead in streamHeads.WithCancellation(cancellation))
            {
                IStream stream;

                try
                {
                    stream = await OpenStreamAsync(streamHead.BucketId, streamHead.StreamId, throwIfNotFound: false, cancellation);
                }
                catch
                {
                    continue; // TODO: Do we really want to silently ignore the exception?
                }

                yield return stream;
            }
        }

        #endregion

        #region IDisposal

        /// <inheritdoc />
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

        private void UpdateCache(Stream stream)
        {
            Debug.Assert(!stream.IsReadOnly);

            lock (_streamsMutex)
            {
                if (!_streams.TryGetValue((stream.BucketId, stream.StreamId, revision: default), out var streamData)
                    || streamData.StreamRevision < stream.StreamRevision)
                {
                    _streams[(stream.BucketId, stream.StreamId, revision: default)] = stream.ToData();
                }
            }
        }

        private sealed class Stream : IStream
        {
            #region Fields

            private readonly ImmutableList<ICommit>.Builder _commits;
            private readonly StreamStore _streamStore;
            private readonly IDateTimeProvider _dateTimeProvider;
            private readonly ILogger _logger;

            #endregion

            #region C'tor

            private Stream(StreamStore streamStore,
                           string bucketId,
                           string streamId,
                           ISnapshot snapshot,
                           IEnumerable<ICommit> commits,
                           bool isFixedRevision,
                           IDateTimeProvider dateTimeProvider,
                           ILogger logger)
            {
                if (streamStore == null)
                    throw new ArgumentNullException(nameof(streamStore));

                if (commits == null)
                    throw new ArgumentNullException(nameof(commits));

                _streamStore = streamStore;
                BucketId = bucketId;
                StreamId = streamId;
                Snapshot = snapshot;
                IsReadOnly = isFixedRevision;
                _dateTimeProvider = dateTimeProvider;
                _logger = logger;

                ExecuteExtensions(commits);

                _commits = (commits as ImmutableList<ICommit>.Builder) ?? (commits as ImmutableList<ICommit>)?.ToBuilder() ?? CreateCommitsBuilder(commits);
            }

            private static ImmutableList<ICommit>.Builder CreateCommitsBuilder(IEnumerable<ICommit> commits)
            {
                var result = ImmutableList.CreateBuilder<ICommit>();
                result.AddRange(commits);
                return result;
            }

            #endregion

            public static async Task<Stream> OpenAsync(StreamStore streamStore,
                                                       string bucketId,
                                                       string streamId,
                                                       long revision,
                                                       IDateTimeProvider dateTimeProvider,
                                                       ILogger logger,
                                                       CancellationToken cancellation)
            {
                if (streamStore == null)
                    throw new ArgumentNullException(nameof(streamStore));

                if (revision < 0)
                    throw new ArgumentOutOfRangeException(nameof(revision));

                var snapshot = await streamStore._persistence.GetSnapshotAsync(bucketId, streamId, revision, cancellation);
                var commits = await streamStore._persistence.GetCommitsAsync(bucketId,
                                                                             streamId, (snapshot?.StreamRevision + 1) ?? default,
                                                                             revision,
                                                                             cancellation).ToListAsync(cancellation)

                    ?? Enumerable.Empty<ICommit>();

                var isFixedRevision = revision != default;

                var result = new Stream(streamStore, bucketId, streamId, snapshot, commits, isFixedRevision, dateTimeProvider, logger);

                if (isFixedRevision && result.StreamRevision != revision)
                {
                    throw new StorageException($"Unable to load stream in revision {revision}.");
                }

                return result;
            }

            public static Stream FromData(StreamStore streamStore,
                                          StreamData streamData,
                                          IDateTimeProvider dateTimeProvider,
                                          ILogger logger)
            {
                var result = new Stream(
                    streamStore,
                    streamData.BucketId,
                    streamData.StreamId,
                    streamData.Snapshot,
                    streamData.Commits,
                    streamData.IsReadOnly,
                    dateTimeProvider,
                    logger);

                return result;
            }

            public StreamData ToData()
            {
                return new StreamData(BucketId, StreamId, Snapshot, Commits, IsReadOnly);
            }

            public string BucketId { get; }
            public string StreamId { get; }
            public bool IsReadOnly { get; }

            public ISnapshot Snapshot { get; private set; }
            public IEnumerable<ICommit> Commits => _commits.ToImmutable();

            public long StreamRevision => Commits.LastOrDefault()?.StreamRevision ?? Snapshot?.StreamRevision ?? 0;
            public IReadOnlyDictionary<string, object> Headers => GetHeaders().ToImmutable();
            public IReadOnlyList<EventMessage> Events => Commits.SelectMany(commit => commit.Events ?? Enumerable.Empty<EventMessage>()).ToImmutableArray();

            private ImmutableDictionary<string, object>.Builder GetHeaders()
            {
                var result = ImmutableDictionary.CreateBuilder<string, object>();
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
                if (IsReadOnly)
                    throw new InvalidOperationException("Cannot modify a read-only stream view.");

                var snapshot = new Snapshot(BucketId, StreamId, StreamRevision, body, Headers/*, ConcurrencyToken*/);

                await _streamStore._persistence.AddSnapshotAsync(snapshot, cancellation);

                Snapshot = snapshot;
                _commits.Clear();
            }

            public async Task<bool> TryCommitAsync(IEnumerable<EventMessage> events,
                                                   object body,
                                                   Action<IDictionary<string, object>> headerGenerator,
                                                   CancellationToken cancellation)
            {
                if (IsReadOnly)
                    throw new InvalidOperationException("Cannot modify a read-only stream view.");

                _logger?.LogDebug(Resources.AttemptingToCommitChanges, StreamId);

                var headers = GetHeaders();
                headerGenerator(headers);

                var commit = await PersistChangesAsync(events,
                                                       body,
                                                       headers.ToImmutable(),
                                                       cancellation);

                // A concurrency conflict occured
                if (commit == null)
                {
                    await UpdateAsync(cancellation);

                    return false;
                }

                _streamStore.UpdateCache(this);

                _logger?.LogDebug($"Commit successfully appended to stream {StreamId}. Dispatching commit.");

                await _streamStore._commitDispatcher.DispatchAsync(commit);

                _logger?.LogDebug($"Commit of stream {StreamId} dispatched successfully.");

                return true;

                throw new StorageException("Gived up on unique concurrency token generation.");
            }

            public async Task<bool> UpdateAsync(CancellationToken cancellation)
            {
                if (IsReadOnly)
                    throw new InvalidOperationException("Cannot modify a read-only stream view.");

                var commits = await _streamStore._persistence.GetCommitsAsync(BucketId, StreamId, StreamRevision + 1, cancellation: cancellation)
                    .ToListAsync(cancellation);

                ExecuteExtensions(commits);

                if (commits.Any())
                {
                    _logger?.LogInformation(Resources.UnderlyingStreamHasChanged, StreamId);
                    _commits.AddRange(commits);
                    _streamStore.UpdateCache(this);

                    return true;
                }

                return false;
            }

            private void ExecuteExtensions(IEnumerable<ICommit> commits)
            {
                foreach (var commit in commits)
                {
                    foreach (var extension in _streamStore._extensions)
                    {
                        extension.OnLoad(commit);
                    }
                }
            }

            private async Task<ICommit> PersistChangesAsync(IEnumerable<EventMessage> events,
                                                            object body,
                                                            IReadOnlyDictionary<string, object> headers,
                                                            CancellationToken cancellation)
            {
                var attempt = BuildCommitAttempt(events, body, headers);

                foreach (var extension in _streamStore._extensions)
                {
                    _logger?.LogDebug("Pushing commit to pre-commit hook of type '{0}'.", extension.GetType());
                    if (!extension.OnCommit(attempt))
                    {
                        _logger?.LogInformation("Pipeline hook of type '{0}' rejected commit attempt.", extension.GetType());
                        return null; // TODO: This will cause the caller to assume a concurrency conflict.
                    }
                }

                _logger?.LogDebug("Pushing attempt on stream '{0}' to the underlying store.", StreamId);
                var commit = await _streamStore._persistence.CommitAsync(attempt, cancellation);

                if (commit != null)
                {
                    try
                    {
                        foreach (var extension in _streamStore._extensions)
                        {
                            _logger?.LogDebug("Pushing commit to post-commit hook of type '{0}'.", extension.GetType());
                            extension.OnCommited(commit);
                        }
                    }
                    finally
                    {
                        _commits.Add(commit);
                    }
                }

                return commit;
            }

            private CommitAttempt BuildCommitAttempt(IEnumerable<EventMessage> events,
                                                     object body,
                                                     IReadOnlyDictionary<string, object> headers)
            {
                _logger?.LogDebug("Building a commit attempt on stream '{0}'.", StreamId);
                return new CommitAttempt(
                    BucketId,
                    StreamId,
                    StreamRevision + 1,
                    _dateTimeProvider.GetCurrentTime(),
                    headers.ToDictionary(x => x.Key, x => x.Value),
                    body,
                    events.ToList());
            }
        }

        private sealed class Snapshot : ISnapshot
        {
            public Snapshot(string bucketId,
                            string streamId,
                            long streamRevision,
                            object payload,
                            IReadOnlyDictionary<string, object> headers)
            {
                BucketId = bucketId;
                StreamId = streamId;
                Payload = payload;
                Headers = headers;
                StreamRevision = streamRevision;
            }

            public string BucketId { get; }

            public string StreamId { get; }

            public object Payload { get; }

            public IReadOnlyDictionary<string, object> Headers { get; }

            public long StreamRevision { get; }
        }

        private sealed class StreamData
        {
            public StreamData(string bucketId, string streamId, ISnapshot snapshot, IEnumerable<ICommit> commits, bool isReadOnly)
            {
                BucketId = bucketId;
                StreamId = streamId;
                Snapshot = snapshot;
                Commits = (commits as ImmutableList<ICommit>) ?? (commits as ImmutableList<ICommit>.Builder)?.ToImmutable() ?? commits.ToImmutableList();
                IsReadOnly = isReadOnly;
            }

            public string BucketId { get; }
            public string StreamId { get; }
            public bool IsReadOnly { get; }

            public ISnapshot Snapshot { get; }
            public ImmutableList<ICommit> Commits { get; }

            public long StreamRevision => Commits.LastOrDefault()?.StreamRevision ?? Snapshot?.StreamRevision ?? 0;
        }
    }
}
