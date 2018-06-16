/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EntityStorageEngine.cs 
 * Types:           (1) AI4E.Storage.Domain.EntityStorageEngine
 *                  (2) AI4E.Storage.Domain.EntityStorageEngine.SnapshotProcessor
 *                  (3) AI4E.Storage.Domain.EntityStorageEngine.CommitDispatcher
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   13.06.2018 
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Storage.Internal;
using AI4E.Storage.Projection;
using JsonDiffPatchDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace AI4E.Storage.Domain
{
    public sealed class EntityStorageEngine : IEntityStorageEngine
    {
        #region Fields

        private readonly IStreamStore _streamStore;
        private readonly IEntityAccessor _entityAccessor;
        private readonly JsonDiffPatch _differ;
        private readonly JsonSerializer _jsonSerializer;

        // TODO: This should be private. Find a good abstraction to access lookup from outside.
        internal readonly Dictionary<(string bucket, string id, long revision), object> _lookup;
        private bool _isDisposed;

        #endregion

        public IEnumerable<(Type type, string id, long revision, object entity)> CachedEntries
        {
            get
            {
                foreach (var entry in _lookup)
                {
                    yield return (type: GetTypeFromBucket(entry.Key.bucket),
                                  entry.Key.id,
                                  entry.Key.revision,
                                  entity: entry.Value);
                }
            }
        }

        #region C'tor

        public EntityStorageEngine(IStreamStore streamStore,
                           IEntityAccessor entityAccessor,
                           ISerializerSettingsResolver serializerSettingsResolver)
        {
            if (streamStore == null)
                throw new ArgumentNullException(nameof(streamStore));

            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (serializerSettingsResolver == null)
                throw new ArgumentNullException(nameof(serializerSettingsResolver));

            _streamStore = streamStore;
            _entityAccessor = entityAccessor;
            _differ = new JsonDiffPatch();
            _jsonSerializer = JsonSerializer.Create(serializerSettingsResolver.ResolveSettings(this));

            _lookup = new Dictionary<(string bucket, string id, long revision), object>();
        }

        #endregion

        private static JToken StreamRoot => JToken.Parse("{}");

        #region IEntityStorageEngine

        public ValueTask<object> GetByIdAsync(Type entityType, string id, CancellationToken cancellation)
        {
            return GetByIdAsync(entityType, id, revision: default, cancellation: cancellation);
        }

        public async ValueTask<object> GetByIdAsync(Type entityType, string id, long revision, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            var bucketId = GetBucketId(entityType);

            if (_lookup.TryGetValue((bucketId, id, revision), out var cachedResult))
                return cachedResult;


            var streamId = id;
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, revision, cancellation);

            var result = Deserialize(entityType, stream);

            _lookup[(bucketId, id, revision)] = result;

            return result;
        }

        public IAsyncEnumerable<object> GetAllAsync(Type entityType, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            var bucketId = GetBucketId(entityType);

            return _streamStore.OpenAllAsync(bucketId, cancellation).Select(stream => CachedDeserialize(entityType, revision: default, stream));
        }

        public IAsyncEnumerable<object> GetAllAsync(CancellationToken cancellation)
        {
            return _streamStore.OpenAllAsync(cancellation).Select(stream => CachedDeserialize(GetTypeFromBucket(stream.BucketId), revision: default, stream));
        }

        public async Task StoreAsync(Type entityType, object entity, string id, CancellationToken cancellation)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or a derived type.", nameof(entity));

            var bucketId = GetBucketId(entityType);
            var streamId = id;
            var concurrencyToken = _entityAccessor.GetConcurrencyToken(entity);

            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, throwIfNotFound: false, cancellation);
            var baseToken = default(JToken);

            if (stream.Snapshot == null)
            {
                baseToken = StreamRoot;
            }
            else
            {
                baseToken = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
            }

            foreach (var commit in stream.Commits)
            {
                baseToken = _differ.Patch(baseToken, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
            }

            var serializedEntity = JToken.FromObject(entity, _jsonSerializer);
            var diff = _differ.Diff(baseToken, serializedEntity);

            var events = _entityAccessor.GetUncommittedEvents(entity).Select(p => new EventMessage { Body = p });

            await stream.CommitAsync(concurrencyToken,
                                     events,
                                     CompressionHelper.Zip(diff.ToString()),
                                     headers => { },
                                     cancellation);

            _entityAccessor.SetConcurrencyToken(entity, stream.ConcurrencyToken);
            _entityAccessor.SetRevision(entity, stream.StreamRevision);
            _entityAccessor.CommitEvents(entity);

            _lookup[(bucketId, _entityAccessor.GetId(entity), revision: default)] = entity;
        }

        public Task DeleteAsync(Type entityType, string id, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            throw new NotSupportedException(); // TODO
        }

        #endregion

        private object Deserialize(Type entityType, IStream stream)
        {
            // This is an empty stream.
            if (stream.StreamRevision == default)
                return null;

            var serializedEntity = default(JToken);

            if (stream.Snapshot == null)
            {
                serializedEntity = StreamRoot;
            }
            else
            {
                serializedEntity = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
            }

            foreach (var commit in stream.Commits)
            {
                serializedEntity = _differ.Patch(serializedEntity, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
            }

            var result = serializedEntity.ToObject(entityType, _jsonSerializer);

            _entityAccessor.SetConcurrencyToken(result, stream.ConcurrencyToken);
            _entityAccessor.SetRevision(result, stream.StreamRevision);
            _entityAccessor.CommitEvents(result);

            return result;
        }

        private object CachedDeserialize(Type entityType, long revision, IStream stream)
        {
            var bucketId = GetBucketId(entityType);

            if (_lookup.TryGetValue((bucketId, stream.StreamId, revision), out var cachedResult))
            {
                return cachedResult;
            }

            var result = Deserialize(entityType, stream);

            _lookup[(bucketId, stream.StreamId, revision)] = result;

            return result;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _streamStore.Dispose();
        }

        #region Helpers

        private static string GetBucketId(Type entityType)
        {
            return entityType.ToString();
        }

        private static Type GetTypeFromBucket(string bucketId)
        {
            return TypeLoadHelper.LoadTypeFromUnqualifiedName(bucketId);
        }

        #endregion

        internal sealed class SnapshotProcessor : ISnapshotProcessor, IAsyncDisposable
        {
            #region Fields

            private readonly StorageOptions _options;
            private readonly IProvider<EntityStorageEngine> _entityStorageEngineProvider;
            private readonly IAsyncProcess _snapshotProcess;

            #endregion

            #region C'tor

            public SnapshotProcessor(IProvider<EntityStorageEngine> entityStorageEngineProvider,
                                     IOptions<StorageOptions> optionsAccessor)
            {
                if (entityStorageEngineProvider == null)
                    throw new ArgumentNullException(nameof(entityStorageEngineProvider));

                if (optionsAccessor == null)
                    throw new ArgumentNullException(nameof(optionsAccessor));

                _options = optionsAccessor.Value ?? new StorageOptions();
                _entityStorageEngineProvider = entityStorageEngineProvider;
                _snapshotProcess = new AsyncProcess(SnapshotProcess);
                _initialization = InitializeInternalAsync(_cancellationSource.Token);
            }

            #endregion

            #region Initialization

            private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();
            private readonly Task _initialization;

            private async Task InitializeInternalAsync(CancellationToken cancellation)
            {
                await _snapshotProcess.StartAsync();
            }

            #endregion

            #region Disposal

            private Task _disposal;
            private readonly TaskCompletionSource<byte> _disposalSource = new TaskCompletionSource<byte>();
            private readonly object _lock = new object();

            public Task Disposal => _disposalSource.Task;

            private async Task DisposeInternalAsync()
            {
                try
                {
                    // Cancel the initialization
                    _cancellationSource.Cancel();
                    try
                    {
                        await _initialization;
                    }
                    catch (OperationCanceledException) { }

                    await _snapshotProcess.TerminateAsync();
                }
                catch (OperationCanceledException) { }
                catch (Exception exc)
                {
                    _disposalSource.SetException(exc);
                    return;
                }

                _disposalSource.SetResult(0);
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposal == null)
                        _disposal = DisposeInternalAsync();
                }
            }

            public Task DisposeAsync()
            {
                Dispose();
                return Disposal;
            }

            #endregion

            private async Task SnapshotProcess(CancellationToken cancellation)
            {
                var interval = _options.SnapshotInterval;

                if (interval < 0)
                    interval = 60 * 60 * 1000;

                while (cancellation.ThrowOrContinue())
                {
                    try
                    {
                        await SnapshotAsync(cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception)
                    {
                        // TODO: Logging
                    }

                    await Task.Delay(_options.SnapshotInterval, cancellation);
                }
            }

            private async Task SnapshotAsync(CancellationToken cancellation)
            {
                var snapshotRevisionThreshold = _options.SnapshotRevisionThreshold;

                if (snapshotRevisionThreshold < 0)
                    snapshotRevisionThreshold = 20;

                using (var entityStorageEngine = _entityStorageEngineProvider.ProvideInstance())
                {
                    var enumerator = default(IAsyncEnumerator<IStream>);
                    try
                    {
                        enumerator = entityStorageEngine._streamStore.OpenStreamsToSnapshotAsync(snapshotRevisionThreshold, cancellation).GetEnumerator();

                        while (await enumerator.MoveNext(cancellation))
                        {
                            var stream = enumerator.Current;

                            if (stream.Snapshot == null && !stream.Commits.Any())
                                continue;

                            var serializedEntity = default(JToken);

                            if (stream.Snapshot == null)
                            {
                                serializedEntity = StreamRoot;
                            }
                            else
                            {
                                serializedEntity = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
                            }

                            foreach (var commit in stream.Commits)
                            {
                                serializedEntity = entityStorageEngine._differ.Patch(serializedEntity, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
                            }

                            await stream.AddSnapshotAsync(CompressionHelper.Zip(serializedEntity.ToString()), cancellation);
                        }
                    }
                    finally
                    {
                        enumerator?.Dispose();
                    }
                }
            }
        }

        internal sealed class CommitDispatcher : ICommitDispatcher, IAsyncDisposable
        {
            #region Fields

            private readonly IProvider<EntityStorageEngine> _entityStorageEngineProvider;
            private readonly IMessageDispatcher _eventDispatcher;
            private readonly IProjectionDependencyStore<string, string> _projectionDependencyStore;
            private readonly IProjector _projector;
            private readonly IDataStore _dataStore;
            private readonly ILogger<CommitDispatcher> _logger;
            private readonly CancellationTokenSource _cancellationSource;
            private readonly Task _initialization;
            private readonly IAsyncProcess _dispatchProcess;
            private readonly AsyncProducerConsumerQueue<(ICommit commit, int attempt, TaskCompletionSource<object> tcs)> _dispatchQueue;
            private readonly IStreamPersistence _persistence;

            #endregion

            #region C'tor

            public CommitDispatcher(IProvider<EntityStorageEngine> entityStorageEngineProvider,
                                    IStreamPersistence persistence,
                                    IMessageDispatcher eventDispatcher,
                                    IProjectionDependencyStore<string, string> projectionDependencyStore,
                                    IProjector projector,
                                    IDataStore dataStore,
                                    ILogger<CommitDispatcher> logger)
            {
                if (entityStorageEngineProvider == null)
                    throw new ArgumentNullException(nameof(entityStorageEngineProvider));

                if (persistence == null)
                    throw new ArgumentNullException(nameof(persistence));

                if (eventDispatcher == null)
                    throw new ArgumentNullException(nameof(eventDispatcher));

                if (projectionDependencyStore == null)
                    throw new ArgumentNullException(nameof(projectionDependencyStore));

                if (projector == null)
                    throw new ArgumentNullException(nameof(projector));

                if (dataStore == null)
                    throw new ArgumentNullException(nameof(dataStore));

                _entityStorageEngineProvider = entityStorageEngineProvider;
                _persistence = persistence;
                _eventDispatcher = eventDispatcher;
                _projectionDependencyStore = projectionDependencyStore;
                _projector = projector;
                _dataStore = dataStore;
                _logger = logger;
                _dispatchQueue = new AsyncProducerConsumerQueue<(ICommit commit, int attempt, TaskCompletionSource<object> tcs)>();
                _cancellationSource = new CancellationTokenSource();
                _dispatchProcess = new AsyncProcess(DispatchProcess);
                _initialization = InitializeInternalAsync(_cancellationSource.Token);
            }

            #endregion

            #region Initialization

            private async Task InitializeInternalAsync(CancellationToken cancellation)
            {
                await _dispatchProcess.StartAsync();

                _logger?.LogDebug(Resources.GettingUndispatchedCommits);
                foreach (var commit in await _persistence.GetUndispatchedCommitsAsync(cancellation))
                {
                    await _dispatchQueue.EnqueueAsync((commit, attempt: 1, tcs: null));
                }
            }

            #endregion

            #region Disposal

            private Task _disposal;
            private readonly TaskCompletionSource<byte> _disposalSource = new TaskCompletionSource<byte>();
            private readonly object _lock = new object();

            public Task Disposal => _disposalSource.Task;

            private async Task DisposeInternalAsync()
            {
                try
                {
                    // Cancel the initialization
                    _cancellationSource.Cancel();
                    try
                    {
                        await _initialization;
                    }
                    catch (OperationCanceledException) { }

                    await _dispatchProcess.TerminateAsync();
                }
                catch (OperationCanceledException) { }
                catch (Exception exc)
                {
                    _disposalSource.SetException(exc);
                    return;
                }

                _disposalSource.SetResult(0);
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposal == null)
                        _disposal = DisposeInternalAsync();
                }
            }

            public Task DisposeAsync()
            {
                Dispose();
                return Disposal;
            }

            #endregion

            private async Task DispatchProcess(CancellationToken cancellation)
            {
                while (cancellation.ThrowOrContinue())
                {
                    var (commit, attempt, tcs) = await _dispatchQueue.DequeueAsync(cancellation);

                    try
                    {
                        var projection = ProjectAsync(commit.BucketId, commit.StreamId, tcs, cancellation);
                        var dispatch = DispatchAsync(commit, cancellation);

                        await Task.WhenAll(projection, dispatch);

                        var success = await dispatch;

                        if (success)
                        {
                            await _persistence.MarkCommitAsDispatchedAsync(commit, cancellation);
                        }
                        else
                        {
                            Reschedule(commit, attempt, cancellation).HandleExceptions();
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                    catch (Exception exc)
                    {
                        // TODO: Log exception

                        Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();

                        Reschedule(commit, attempt, cancellation).HandleExceptions();
                    }
                }
            }

            private async Task Reschedule(ICommit commit, int attempt, CancellationToken cancellation)
            {
                // Calculate wait time in seconds
                var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

                await Task.Delay(timeToWait);

                await _dispatchQueue.EnqueueAsync((commit, attempt + 1, tcs: null), cancellation);
            }

            // Adapted from: https://stackoverflow.com/questions/383587/how-do-you-do-integer-exponentiation-in-c
            private static int Pow(int x, int pow)
            {
                if (pow < 0)
                    throw new ArgumentOutOfRangeException(nameof(pow));

                var result = 1;
                while (pow != 0)
                {
                    if ((pow & 1) == 1)
                        result *= x;
                    x *= x;
                    pow >>= 1;
                }

                if (result < 0)
                    return int.MaxValue;

                return result;
            }

            private async Task<bool> DispatchAsync(ICommit commit, CancellationToken cancellation)
            {
                var events = commit.Events.Select(p => p.Body);

                var dispatchResults = await Task.WhenAll(events.Select(p => DispatchSingleAsync(p, cancellation)));
                var dispatchResult = new AggregateDispatchResult(dispatchResults);

                // TODO: Log errors
                return dispatchResult.IsSuccess; // TODO: Throw an exception instead?
            }

            private Task<IDispatchResult> DispatchSingleAsync(object evt, CancellationToken cancellation)
            {
                return _eventDispatcher.DispatchAsync(evt, publish: true);
            }

            private async Task ProjectAsync(string bucketId, string id, TaskCompletionSource<object> tcs, CancellationToken cancellation)
            {
                var type = GetTypeFromBucket(bucketId);
                if (type == null)
                {
                    throw new InvalidOperationException($"Unable to load type for bucket '{bucketId}'");
                }

                using (var entityStorageEngine = _entityStorageEngineProvider.ProvideInstance())
                {
                    async Task ProjectLocalAsync()
                    {
                        try
                        {
                            await ProjectSingleAsync(type, id, entityStorageEngine, cancellation);
                            Task.Run(() => tcs?.TrySetResult(null)).HandleExceptions();
                        }
                        catch (Exception exc)
                        {
                            Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();
                            throw;
                        }
                    }

                    var tasks = new List<Task>
                    {
                        ProjectLocalAsync()
                    };

                    var dependents = await _projectionDependencyStore.GetDependentsAsync(GetBucketId(type), id, cancellation);

                    tasks.AddRange(dependents.Select(p => ProjectSingleAsync(GetTypeFromBucket(p.BucketId), p.Id, null, cancellation)));

                    await Task.WhenAll(tasks);

                    var oldDependencies = (await _projectionDependencyStore.GetDependenciesAsync(GetBucketId(type), id, cancellation)).Select(p => (bucket: p.BucketId, id: p.Id));
                    var dependencies = entityStorageEngine._lookup.Where(p => !(p.Key.id.Equals(id) && p.Key.revision == default && p.Value.GetType() == type)).Select(p => (bucket: GetBucketId(p.Value.GetType()), p.Key.id));

                    var added = dependencies.Except(oldDependencies);
                    var removed = oldDependencies.Except(dependencies);

                    foreach (var a in added)
                    {
                        await _projectionDependencyStore.AddDependencyAsync(GetBucketId(type), id, a.bucket, a.id, cancellation);
                    }

                    foreach (var a in removed)
                    {
                        await _projectionDependencyStore.RemoveDependencyAsync(GetBucketId(type), id, a.bucket, a.id, cancellation);
                    }
                }
            }

            private async Task ProjectSingleAsync(Type type,
                                                  string id,
                                                  EntityStorageEngine entityStorageEngine,
                                                  CancellationToken cancellation)
            {
                var entity = default(object);

                if (entityStorageEngine == null)
                {
                    using (entityStorageEngine = _entityStorageEngineProvider.ProvideInstance())
                    {
                        entity = await entityStorageEngine.GetByIdAsync(type, id, cancellation: default);
                    }
                }
                else
                {
                    entity = await entityStorageEngine.GetByIdAsync(type, id, cancellation: default);
                }

                var projectionResults = await _projector.ProjectAsync(entity.GetType(), entity, cancellation);

                foreach (var projectionResult in projectionResults)
                {
                    await _dataStore.StoreAsync(projectionResult.ResultType, projectionResult.Result, cancellation);
                }
            }

            public Task DispatchAsync(ICommit commit)
            {
                var tcs = new TaskCompletionSource<object>();

                return Task.WhenAll(_dispatchQueue.EnqueueAsync((commit, attempt: 1, tcs)), tcs.Task);
            }
        }
    }
}
