﻿/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EntityStore.cs 
 * Types:           (1) AI4E.Storage.EntityStore'3
 *                  (2) AI4E.Storage.EntityStore'3.SnapshotProcessor
 *                  (3) AI4E.Storage.EntityStore'3.CommitDispatcher
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.03.2018 
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.DispatchResults;
using AI4E.Processing;
using AI4E.Storage.Projection;
using JsonDiffPatchDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;

namespace AI4E.Storage
{
    public sealed class EntityStore<TId, TEventBase, TEntityBase> : IEntityStore<TId, TEventBase, TEntityBase>
        where TId : struct, IEquatable<TId>
        where TEventBase : class
        where TEntityBase : class
    {
        #region Fields

        private readonly IEventStore<string, TId> _streamStore;
        private readonly IEntityAccessor<TId, TEventBase, TEntityBase> _entityAccessor;
        private readonly JsonDiffPatch _differ;
        private readonly JsonSerializer _jsonSerializer;
        private readonly Dictionary<(TId id, long revision), TEntityBase> _lookup = new Dictionary<(TId id, long revision), TEntityBase>();
        private bool _isDisposed;

        #endregion

        #region C'tor

        public EntityStore(IEventStore<string, TId> streamStore,
                           IEntityAccessor<TId, TEventBase, TEntityBase> entityAccessor,
                           ISerializerSettingsResolver<TId, TEventBase, TEntityBase> serializerSettingsResolver)
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
        }

        #endregion

        public Task<TEntityBase> GetByIdAsync(Type entityType, TId id, CancellationToken cancellation)
        {
            return GetByIdAsync(entityType, id, revision: default, cancellation: cancellation);
        }

        public async Task<TEntityBase> GetByIdAsync(Type entityType, TId id, long revision, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (!typeof(TEntityBase).IsAssignableFrom(entityType))
                throw new ArgumentException($"The argument must specify a subtype of '{typeof(TEntityBase).FullName}'.", nameof(entityType));

            if (_lookup.TryGetValue((id, revision), out var cachedResult))
                return cachedResult;

            var bucketId = GetBucketId(entityType);
            var streamId = GetStreamId(id);
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, revision, cancellation);

            // This is an empty stream.
            if (stream.StreamRevision == default)
                return null;

            var serializedEntity = default(JToken);

            if (stream.Snapshot == null)
            {
                serializedEntity = JToken.Parse("{}");
            }
            else
            {
                serializedEntity = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
            }

            foreach (var commit in stream.Commits)
            {
                serializedEntity = _differ.Patch(serializedEntity, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
            }

            var result = (TEntityBase)serializedEntity.ToObject(entityType, _jsonSerializer);

            _entityAccessor.SetConcurrencyToken(result, stream.ConcurrencyToken);
            _entityAccessor.SetRevision(result, stream.StreamRevision);
            _entityAccessor.CommitEvents(result);

            _lookup[(id, revision)] = result;

            return result;
        }

        public async Task StoreAsync(Type entityType, TEntityBase entity, CancellationToken cancellation)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (!typeof(TEntityBase).IsAssignableFrom(entityType))
                throw new ArgumentException($"The argument must specify a subtype of '{typeof(TEntityBase).FullName}'.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The argument must specify a basetype of '{entity.GetType()}'.", nameof(entityType));

            var bucketId = GetBucketId(entityType);
            var streamId = GetStreamId(entity);
            var concurrencyToken = _entityAccessor.GetConcurrencyToken(entity);

            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, cancellation);
            var baseToken = default(JToken);

            if (stream.Snapshot == null)
            {
                baseToken = JToken.Parse("{}");
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

            _lookup[(_entityAccessor.GetId(entity), revision: default)] = entity;
        }

        public Task<IEnumerable<TEntityBase>> GetAllAsync(Type entityType, CancellationToken cancellation)
        {
            throw new NotSupportedException(); // TODO
        }

        public Task<IEnumerable<TEntityBase>> GetAllAsync(CancellationToken cancellation)
        {
            throw new NotSupportedException(); // TODO
        }

        public Task DeleteAsync(Type entityType, TEntityBase entity, CancellationToken cancellation)
        {
            throw new NotSupportedException(); // TODO
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

        #region Generic overloads

        // TODO: When C#8 releases, these overloads should be put either into the interface or an extension.

        public async Task<TEntity> GetByIdAsync<TEntity>(TId id, CancellationToken cancellation)
            where TEntity : class, TEntityBase
        {
            return (TEntity)await GetByIdAsync(typeof(TEntity), id, cancellation);
        }

        public async Task<TEntity> GetByIdAsync<TEntity>(TId id, long revision, CancellationToken cancellation)
            where TEntity : class, TEntityBase
        {
            return (TEntity)await GetByIdAsync(typeof(TEntity), id, revision, cancellation);
        }

        public Task StoreAsync<TEntity>(TEntity entity, CancellationToken cancellation)
                    where TEntity : class, TEntityBase
        {
            return StoreAsync(typeof(TEntity), entity, cancellation);
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync<TEntity>(CancellationToken cancellation)
            where TEntity : class, TEntityBase
        {
            return (await GetAllAsync(typeof(TEntity), cancellation)).Cast<TEntity>();
        }

        public Task DeleteAsync<TEntity>(TEntity entity, CancellationToken cancellation)
            where TEntity : class, TEntityBase
        {
            return DeleteAsync(typeof(TEntity), entity, cancellation);
        }

        #endregion

        #region Helpers

        private static string GetBucketId(Type entityType)
        {
            return entityType.FullName;
        }

        private static Type GetTypeFromBucket(string bucketId)
        {
            Debug.Assert(bucketId != null);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var t = assembly.GetType(bucketId, false);

                if (t != null)
                    return t;
            }
            throw new ArgumentException("Type '" + bucketId + "' doesn't exist.");
        }

        private TId GetStreamId(TEntityBase entity)
        {
            return GetStreamId(_entityAccessor.GetId(entity));
        }

        private TId GetStreamId(TId id)
        {
            return id;
        }

        #endregion

        internal sealed class SnapshotProcessor : ISnapshotProcessor<string, TId>, IAsyncDisposable
        {
            #region Fields

            private readonly StorageOptions _options;
            private readonly IProvider<EntityStore<TId, TEventBase, TEntityBase>> _entityStoreProvider;
            private readonly IAsyncProcess _snapshotProcess;

            #endregion

            #region C'tor

            public SnapshotProcessor(IProvider<EntityStore<TId, TEventBase, TEntityBase>> entityStoreProvider,
                                     IOptions<StorageOptions> optionsAccessor)
            {
                if (entityStoreProvider == null)
                    throw new ArgumentNullException(nameof(entityStoreProvider));

                if (optionsAccessor == null)
                    throw new ArgumentNullException(nameof(optionsAccessor));

                _options = optionsAccessor.Value ?? new StorageOptions();
                _entityStoreProvider = entityStoreProvider;
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

                using (var entityStore = _entityStoreProvider.ProvideInstance())
                {
                    foreach (var stream in await entityStore._streamStore.OpenStreamsToSnapshotAsync(snapshotRevisionThreshold, cancellation))
                    {
                        if (stream.Snapshot == null && !stream.Commits.Any())
                            continue;

                        var serializedEntity = default(JToken);

                        if (stream.Snapshot == null)
                        {
                            serializedEntity = JToken.Parse("{}");
                        }
                        else
                        {
                            serializedEntity = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
                        }

                        foreach (var commit in stream.Commits)
                        {
                            serializedEntity = entityStore._differ.Patch(serializedEntity, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
                        }

                        await stream.AddSnapshotAsync(CompressionHelper.Zip(serializedEntity.ToString()), cancellation);
                    }
                }
            }
        }

        internal sealed class CommitDispatcher : ICommitDispatcher<string, TId>, IAsyncDisposable
        {
            #region Fields

            private readonly IProvider<EntityStore<TId, TEventBase, TEntityBase>> _entityStoreProvider;
            private readonly IMessageDispatcher _eventDispatcher;
            private readonly IProjectionDependencyStore<string, TId> _projectionDependencyStore;
            private readonly IProjector _projector;
            private readonly ILogger<CommitDispatcher> _logger;
            private readonly CancellationTokenSource _cancellationSource;
            private readonly Task _initialization;
            private readonly IAsyncProcess _dispatchProcess;
            private readonly AsyncProducerConsumerQueue<(ICommit<string, TId> commit, int attempt, TaskCompletionSource<object> tcs)> _dispatchQueue;
            private readonly IStreamPersistence<string, TId> _persistence;

            #endregion

            #region C'tor

            public CommitDispatcher(IProvider<EntityStore<TId, TEventBase, TEntityBase>> entityStoreProvider,
                                          IStreamPersistence<string, TId> persistence,
                                          IMessageDispatcher eventDispatcher,
                                          IProjectionDependencyStore<string, TId> projectionDependencyStore,
                                          IProjector projector,
                                          ILogger<CommitDispatcher> logger)
            {
                if (entityStoreProvider == null)
                    throw new ArgumentNullException(nameof(entityStoreProvider));

                if (persistence == null)
                    throw new ArgumentNullException(nameof(persistence));

                if (eventDispatcher == null)
                    throw new ArgumentNullException(nameof(eventDispatcher));

                if (projectionDependencyStore == null)
                    throw new ArgumentNullException(nameof(projectionDependencyStore));

                if (projector == null)
                    throw new ArgumentNullException(nameof(projector));

                _entityStoreProvider = entityStoreProvider;
                _persistence = persistence;
                _eventDispatcher = eventDispatcher;
                _projectionDependencyStore = projectionDependencyStore;
                _projector = projector;
                _logger = logger;
                _dispatchQueue = new AsyncProducerConsumerQueue<(ICommit<string, TId> commit, int attempt, TaskCompletionSource<object> tcs)>();
                _cancellationSource = new CancellationTokenSource();
                _dispatchProcess = new AsyncProcess(DispatchProcess);
                _initialization = InitializeInternalAsync(_cancellationSource.Token);
            }

            public CommitDispatcher(IProvider<EntityStore<TId, TEventBase, TEntityBase>> entityStoreProvider,
                                    IStreamPersistence<string, TId> persistence,
                                    IMessageDispatcher eventDispatcher,
                                    IProjectionDependencyStore<string, TId> projectionDependencyStore,
                                    IProjector projector)
                : this(entityStoreProvider, persistence, eventDispatcher, projectionDependencyStore, projector, null) { }

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

            private async Task Reschedule(ICommit<string, TId> commit, int attempt, CancellationToken cancellation)
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

            private async Task<bool> DispatchAsync(ICommit<string, TId> commit, CancellationToken cancellation)
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

            private async Task ProjectAsync(string bucketId, TId id, TaskCompletionSource<object> tcs, CancellationToken cancellation)
            {
                var type = GetTypeFromBucket(bucketId);
                if (type == null)
                {
                    throw new InvalidOperationException($"Unable to load type for bucket '{bucketId}'");
                }

                using (var entityStore = _entityStoreProvider.ProvideInstance())
                {
                    async Task ProjectLocalAsync()
                    {
                        try
                        {
                            await ProjectSingleAsync(type, id, entityStore, cancellation);
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
                    var dependencies = entityStore._lookup.Where(p => !(p.Key.id.Equals(id) && p.Key.revision == default && p.Value.GetType() == type)).Select(p => (bucket: GetBucketId(p.Value.GetType()), p.Key.id));

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

            private async Task ProjectSingleAsync(Type type, TId id, EntityStore<TId, TEventBase, TEntityBase> entityStore, CancellationToken cancellation)
            {
                var entity = default(TEntityBase);

                if (entityStore == null)
                {
                    using (entityStore = _entityStoreProvider.ProvideInstance())
                    {
                        entity = await entityStore.GetByIdAsync(type, id, cancellation: default);
                    }
                }
                else
                {
                    entity = await entityStore.GetByIdAsync(type, id, cancellation: default);
                }

                await _projector.ProjectAsync(entity.GetType(), entity, cancellation);
            }

            public Task DispatchAsync(ICommit<string, TId> commit)
            {
                var tcs = new TaskCompletionSource<object>();

                return Task.WhenAll(_dispatchQueue.EnqueueAsync((commit, attempt: 1, tcs)), tcs.Task);
            }
        }
    }
}
