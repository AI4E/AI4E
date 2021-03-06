﻿/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// TODO: Register as application service.

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityStorageEngine"/>
    public sealed class EntityStorageEngine : IEntityStorageEngine
    {
        private readonly IDatabase _database;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IOptions<DomainStorageOptions> _optionsAccessor;
        private readonly ILogger<EntityStorageEngine> _logger;

        private readonly EntityStorageEngineCache _cache;
        private readonly AsyncInitializationHelper _initHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityStorageEngine"/> type.
        /// </summary>
        /// <param name="database">The underlying database abstraction.</param>
        /// <param name="eventDispatcher">The domain-event dispatcher that is used to dispatch domain-events.</param>
        /// <param name="optionsAccessor">
        /// An <see cref="IOptions{DomainStorageOptions}"/> that is used to resolve domain storage options.
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger{EntityStorageEngine}"/> used for logging or <c>null</c> to disable logging.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="database"/>, <paramref name="eventDispatcher"/> 
        /// or <paramref name="optionsAccessor"/> is <c>null</c>.
        /// </exception>
        public EntityStorageEngine(
            IDatabase database,
            IDomainEventDispatcher eventDispatcher,
            IOptions<DomainStorageOptions> optionsAccessor,
            ILogger<EntityStorageEngine>? logger = null)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            if (eventDispatcher is null)
                throw new ArgumentNullException(nameof(eventDispatcher));

            if (optionsAccessor is null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _database = database;
            _eventDispatcher = eventDispatcher;
            _optionsAccessor = optionsAccessor;
            _logger = logger ?? new NullLogger<EntityStorageEngine>();

            _cache = new EntityStorageEngineCache();
            _initHelper = new AsyncInitializationHelper(InitInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region Event-dispatch

        private ValueTask RegisterForDispatchAsync(
            IReadOnlyList<StoredDomainEventBatch> batches,
            CancellationToken cancellation)
        {
            _logger.LogDebug(
                Resources.EngineDispatchingMultipleDomainEventBatches,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var dispatchTasks = new List<ValueTask>(capacity: batches.Count);

            for (var i = 0; i < batches.Count; i++)
            {
#pragma warning disable CA2012
                dispatchTasks.Add(RegisterForDispatchAsync(batches[i], cancellation));
#pragma warning restore CA2012
            }

            return dispatchTasks.WhenAll();
        }

        private async ValueTask RegisterForDispatchAsync(StoredDomainEventBatch batch, CancellationToken cancellation)
        {
            _logger.LogTrace(
               Resources.EngineDispatchingDomainEventBatch,
               new EntityIdentifier(batch.EntityType, batch.EntityId),
               _optionsAccessor.Value.Scope ?? Resources.NoScope);

            // Dispatch the batch
            var tasks = new List<ValueTask>(capacity: batch.DomainEvents.Count);

            foreach (var domainEvent in batch.DomainEvents)
            {
                tasks.Add(
#pragma warning disable CA2012
                    _eventDispatcher.DispatchAsync(
                        new DomainEvent(domainEvent.EventType, domainEvent.Event), cancellation));
#pragma warning restore CA2012
            }

            await tasks.WhenAll().ConfigureAwait(false);

            // Mark the batch as dispatched
            await MarkAsDispatchedAsync(batch, cancellation).ConfigureAwait(false);

            _logger.LogTrace(
               Resources.EngineDispatchedDomainEventBatch,
               new EntityIdentifier(batch.EntityType, batch.EntityId),
               _optionsAccessor.Value.Scope ?? Resources.NoScope);
        }

        private async ValueTask MarkAsDispatchedAsync(StoredDomainEventBatch batch, CancellationToken cancellation)
        {
            _logger.LogTrace(
               Resources.EngineMarkingDomainEventBatchAsDispatched,
               new EntityIdentifier(batch.EntityType, batch.EntityId),
               _optionsAccessor.Value.Scope ?? Resources.NoScope);

            if (!batch.EntityDeleted)
            {
                _logger.LogTrace(
                    Resources.EngineDeletingDomainEventBatchFromDatabase,
                    new EntityIdentifier(batch.EntityType, batch.EntityId),
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                await _database.RemoveAsync(batch, cancellation).ConfigureAwait(false);
            }
            else
            {
                _logger.LogTrace(
                    Resources.EngineDeletingDomainEventBatchAndEntityFromDatabase,
                    new EntityIdentifier(batch.EntityType, batch.EntityId),
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                using var databaseScope = _database.CreateScope();

                do
                {
                    await databaseScope.RemoveAsync(batch, cancellation).ConfigureAwait(false);
                    var storedEntity = await StoredEntityHelper.LoadStoredEntityAsync(
                        batch.EntityType,
                        databaseScope,
                        _optionsAccessor.Value.Scope,
                        batch.EntityId,
                        cancellation).ConfigureAwait(false);

                    if (storedEntity != null
                        && storedEntity.IsMarkedAsDeleted
                        && storedEntity.Epoch == batch.EntityEpoch)
                    {
                        await StoredEntityHelper.RemoveStoredEntityAsync(databaseScope, storedEntity, cancellation)
                            .ConfigureAwait(false);

                    }

                }
                while (!await databaseScope.TryCommitAsync(cancellation).ConfigureAwait(false));
            }
        }

        #endregion

        #region Initialization

        private async Task InitInternalAsync(CancellationToken cancellation)
        {
            _logger.LogTrace(
              Resources.EngineInitializing,
              _optionsAccessor.Value.Scope ?? Resources.NoScope);

            static Expression<Func<StoredDomainEventBatch, bool>> BuildPredicate(string? scope)
            {
                if (scope is null)
                {
                    return _ => true;
                }
                else
                {
                    return p => p.Scope == scope;
                }
            }

            try
            {
                // Load all undispatched domain event batches
                var batches = _database.GetAsync(BuildPredicate(_optionsAccessor.Value.Scope), cancellation);
                var valueTasks = new List<ValueTask>();

                await foreach (var eventBatch in batches)
                {
#pragma warning disable CA2012
                    valueTasks.Add(RegisterForDispatchAsync(eventBatch, cancellation));
#pragma warning restore CA2012
                }

                await valueTasks.WhenAll().ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031
            catch (Exception exc)
#pragma warning restore CA1031
            {
                _logger.LogCritical(
                    exc,
                    Resources.EngineInitializationFailed,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

#pragma warning disable VSTHRD103
                // This is guaranteed not to block
                _disposeHelper.Dispose();
#pragma warning restore VSTHRD103
            }

            _logger.LogTrace(
              Resources.EngineInitialized,
              _optionsAccessor.Value.Scope ?? Resources.NoScope);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _initHelper.CancelAsync().ConfigureAwait(false);
        }

        #endregion

        /// <inheritdoc/>
        public async ValueTask<IEntityQueryResult> QueryEntityAsync(
            EntityIdentifier entityIdentifier,
            bool bypassCache,
            CancellationToken cancellation)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                cancellation = guard.Cancellation;

                if (entityIdentifier == default)
                {
                    _logger.LogWarning(
                        Resources.EngineLoadingDefaultEntityIdentifier,
                        _optionsAccessor.Value.Scope ?? Resources.NoScope);

                    return new NotFoundEntityQueryResult(
                        entityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance);
                }

                return await UncheckedLoadEntityAsync(entityIdentifier, bypassCache, cancellation)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private ValueTask<IEntityQueryResult> UncheckedLoadEntityAsync(
            EntityIdentifier entityIdentifier,
            bool bypassCache,
            CancellationToken cancellation)
        {
            if (!bypassCache
                && _cache.TryGetFromCache(entityIdentifier, out var entityLoadResult))
            {
                _logger.LogDebug(
                    Resources.EngineLoadingEntityFromCache,
                    entityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                return new ValueTask<IEntityQueryResult>(entityLoadResult);
            }

            return UncachedLoadEntityAsync(entityIdentifier, cancellation);
        }

        private async ValueTask<IEntityQueryResult> UncachedLoadEntityAsync(
            EntityIdentifier entityIdentifier,
            CancellationToken cancellation)
        {
            _logger.LogDebug(
                Resources.EngineLoadingEntityFromDatabase,
                entityIdentifier,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var (entityLoadResult, epoch) = await StoredEntityHelper.LoadEntityAsync(
                entityIdentifier.EntityType,
                _database,
                _optionsAccessor.Value.Scope,
                entityIdentifier.EntityId,
                cancellation).ConfigureAwait(false);

            _cache.UpdateCache(entityLoadResult, epoch);

            if (entityLoadResult.IsFound())
            {
                _logger.LogDebug(
                    Resources.EngineLoadedEntityFromDatabase,
                    entityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);
            }
            else
            {
                _logger.LogDebug(
                    Resources.EngineFailureLoadingEntityFromDatabase,
                    entityIdentifier,
                    entityLoadResult.Reason ?? Resources.UnknownReason,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);
            }

            return entityLoadResult;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IFoundEntityQueryResult> QueryEntitiesAsync(
            Type entityType,
            bool bypassCache,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            EntityValidationHelper.Validate(entityType);

            using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
            cancellation = guard.Cancellation;

            _logger.LogDebug(
                Resources.EngineLoadingEntitiesFromDatabase,
                entityType,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            IAsyncEnumerable<(IFoundEntityQueryResult entityQueryResult, int epoch)> entityLoadResults;

            try
            {
                entityLoadResults = StoredEntityHelper.LoadEntitiesAsync(
                    entityType, _database, _optionsAccessor.Value.Scope, cancellation);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            void CatchResultException(OperationCanceledException exception)
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                ExceptionDispatchInfo.Capture(exception).Throw();
            }

            entityLoadResults = entityLoadResults.Catch((Action<OperationCanceledException>)CatchResultException);

            await foreach (var (entityLoadResult, epoch) in entityLoadResults.WithCancellation(cancellation))
            {
                _logger.LogTrace(
                    Resources.EngineProcessingEntity,
                    entityLoadResult.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                _cache.UpdateCache(entityLoadResult, epoch);

                yield return entityLoadResult;
            }

            _logger.LogDebug(
                Resources.EngineLoadedEntitiesFromDatabase,
                entityType,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);
        }

        /// <inheritdoc/>
        public async ValueTask<EntityCommitResult> CommitAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            _logger.LogDebug(
                Resources.EngineProcessingCommitAttempt, _optionsAccessor.Value.Scope ?? Resources.NoScope);

            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                using var databaseScope = _database.CreateScope();
                cancellation = guard.Cancellation;

                bool concurrencyFailure;
                var eventBatches = new List<StoredDomainEventBatch>();
                var entriesToUpdateCache = new List<(TCommitAttemptEntry entry, int epoch)>();
                var firstRun = true;

                do
                {
                    // Check concurrency on local cache and update local cache if concurrency check fails.
                    if (!await CheckConcurrencyAsync(commitAttempt, cancellation).ConfigureAwait(false))
                    {
                        _logger.LogDebug(
                            Resources.EngineCommitAttemptConcurrencyCheckFailed,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);

                        return EntityCommitResult.ConcurrencyFailure;
                    }

                    if (firstRun)
                    {
                        firstRun = false;

                        _logger.LogTrace(
                            Resources.EngineStartedCommitTransaction,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);
                    }
                    else
                    {
                        _logger.LogTrace(
                            Resources.EngineFailedToCommitTransaction,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);
                    }

                    concurrencyFailure = false;
                    eventBatches.Clear();
                    entriesToUpdateCache.Clear();

                    foreach (var entry in commitAttempt.Entries)
                    {
                        var storedEntity = await StoredEntityHelper.LoadStoredEntityAsync(
                            entry.EntityIdentifier.EntityType,
                            databaseScope,
                            _optionsAccessor.Value.Scope,
                            entry.EntityIdentifier.EntityId,
                            cancellation).ConfigureAwait(false);

                        var storedEntityRevision = storedEntity?.Revision ?? 0;

                        if (storedEntity != null && storedEntity.IsMarkedAsDeleted)
                        {
                            storedEntityRevision = 0;
                        }

                        // Skip concurrency checks for append events only operations.
                        if (entry.Operation != CommitOperation.AppendEventsOnly &&
                            storedEntityRevision != entry.ExpectedRevision)
                        {
                            concurrencyFailure = true;
                            await databaseScope.RollbackAsync(cancellation).ConfigureAwait(false);

                            // As the concurrency check based on the entries in the cache succeeded, but the comparison
                            // with the true values does not, we have to update the cache, to prevent a situation that
                            // we check the (unchanged) cache again, which leads to the exact same situation, thus
                            // an infinite cycle.

                            // TODO: This is a copy
                            IEntityQueryResult entityQueryResult;

                            if (storedEntity is null)
                            {
                                entityQueryResult = new NotFoundEntityQueryResult(
                                    entry.EntityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance);
                            }
                            else
                            {
                                entityQueryResult = new FoundEntityQueryResult(
                                    entry.EntityIdentifier,
                                    storedEntity.Entity!,
                                    storedEntity.ConcurrencyToken,
                                    storedEntity.Revision,
                                    loadedFromCache: false,
                                    EntityQueryResultGlobalScope.Instance);
                            }

                            _cache.UpdateCache(entityQueryResult, storedEntity.Epoch);

                            break;
                        }

                        // We need to be able to uniquely identify a domain event that is not fully dispatched.
                        // To guarantee this, we include the id and revision of the entity in the domain event's is that
                        // raised the domain event.
                        // If an entity is deleted and afterwards either deleted again or created/updated, 
                        // succeeding domain-events may get the same combination of id and revision as the revision is 
                        // restarted whenever an entity is deleted and recreated.
                        // As a solution we add an epoch counter to the stored-entity that is increased each time, 
                        // an entity is recreated. This allows to create a unique id for domain events when the epoch is 
                        // included in the id generation process. We do no expect the epoch counter to overflow.
                        // When all domain events of all epochs of an entity are dispatched, we can get rid of the 
                        // stored-entity as domain event is generation is guaranteed to be unique now.
                        if (entry.Operation == CommitOperation.Delete)
                        {
                            if (entry.DomainEvents.Count != 0 || storedEntity != null && storedEntity.IsMarkedAsDeleted)
                            {
                                _logger.LogTrace(
                                    Resources.EngineStoringEntityMarkedAsDeletedToDatabase,
                                    entry.EntityIdentifier,
                                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                                storedEntity ??= StoredEntityHelper.CreateStoredEntity(
                                    entry.EntityIdentifier, _optionsAccessor.Value.Scope);
                                storedEntity.Revision = entry.Revision;
                                storedEntity.ConcurrencyToken = entry.ConcurrencyToken.ToString();

                                if (storedEntity.IsMarkedAsDeleted)
                                {
                                    storedEntity.Epoch++;
                                }

                                storedEntity.IsMarkedAsDeleted = true;
                                storedEntity.Entity = null;
                                await StoredEntityHelper.StoreStoredEntityAsync(databaseScope, storedEntity, cancellation)
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                _logger.LogTrace(
                                    Resources.EngineDeletetingEntityFromDatabase,
                                    entry.EntityIdentifier,
                                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                                if (storedEntity != null)
                                {
                                    await StoredEntityHelper.RemoveStoredEntityAsync(
                                        databaseScope, storedEntity, cancellation).ConfigureAwait(false);
                                }
                            }

                            entriesToUpdateCache.Add((entry, storedEntity.Epoch));
                        }
                        else if (entry.Operation == CommitOperation.Store)
                        {
                            _logger.LogTrace(
                                    Resources.EngineStoringEntityToDatabase,
                                    entry.EntityIdentifier,
                                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                            storedEntity ??= StoredEntityHelper.CreateStoredEntity(
                                entry.EntityIdentifier, _optionsAccessor.Value.Scope);
                            storedEntity.Revision = entry.Revision;
                            storedEntity.ConcurrencyToken = entry.ConcurrencyToken.ToString();

                            if (storedEntity.IsMarkedAsDeleted)
                            {
                                storedEntity.Epoch++;
                                storedEntity.IsMarkedAsDeleted = false;
                            }

                            storedEntity.Entity = entry.Entity;
                            await StoredEntityHelper.StoreStoredEntityAsync(databaseScope, storedEntity, cancellation)
                                .ConfigureAwait(false);

                            entriesToUpdateCache.Add((entry, storedEntity.Epoch));
                        }

                        if (!concurrencyFailure && entry.DomainEvents.Count != 0)
                        {
                            _logger.LogTrace(
                                Resources.EngineWritingDomainEventBatchToDatabase,
                                entry.EntityIdentifier,
                                _optionsAccessor.Value.Scope ?? Resources.NoScope);

                            var eventBatch = StoredDomainEventBatch.Create(
                                entry, storedEntity?.Epoch ?? 0, _optionsAccessor.Value.Scope);
                            eventBatches.Add(eventBatch);
                            await databaseScope.StoreAsync(eventBatch, cancellation).ConfigureAwait(false);
                        }
                    }
                }
                while (concurrencyFailure || !await databaseScope.TryCommitAsync(cancellation).ConfigureAwait(false));

                _logger.LogDebug(
                    Resources.EngineCommitSuccess,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                foreach (var (entry, epoch) in entriesToUpdateCache)
                {
                    IEntityQueryResult queryResult;

                    if (entry.Operation == CommitOperation.Delete)
                    {
                        queryResult = new NotFoundEntityQueryResult(
                            entry.EntityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance);
                    }
                    else if (entry.Operation == CommitOperation.Store)
                    {
                        queryResult = new FoundEntityQueryResult(
                            entry.EntityIdentifier,
                            entry.Entity!,
                            entry.ConcurrencyToken,
                            entry.Revision,
                            loadedFromCache: false,
                            EntityQueryResultGlobalScope.Instance);
                    }
                    else
                    {
                        continue;
                    }

                    _cache.UpdateCache(queryResult, epoch);
                }

                // Add event-batches to dispatch queue
                var task = RegisterForDispatchAsync(eventBatches, cancellation: default).WithCancellation(cancellation);

                if (_optionsAccessor.Value.SynchronousEventDispatch)
                {
                    await task.ConfigureAwait(false);
                }

                return EntityCommitResult.Success;
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private static bool CheckConcurrency<TCommitAttemptEntry>(
            TCommitAttemptEntry entry,
            IEntityLoadResult entityLoadResult)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            Debug.Assert(entry.EntityIdentifier == entityLoadResult.EntityIdentifier);

            var expectedRevision = entry.ExpectedRevision;
            var actualRevision = entityLoadResult.Revision;
            return expectedRevision == actualRevision;
        }

        // If the entry in the cache is of another epoch as the one in the database but with the same revision,
        // the concurrency-check succeeds, as we do not care here whether the epoch is the same nor does the 
        // check in the database do. Revision is all we care about.
        private async ValueTask<bool> CheckConcurrencyAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            foreach (var entry in commitAttempt.Entries)
            {
                // Skip concurrency checks for append events only operations.
                if (entry.Operation == CommitOperation.AppendEventsOnly)
                    continue;

                _logger.LogTrace(
                    Resources.EngineCheckingConcurrency,
                    entry.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                // We do not need to copy the load result as the load result itself is thread-safe, 
                // while the entity included is not.
                var entityLoadResult = await UncheckedLoadEntityAsync(
                    entry.EntityIdentifier,
                    bypassCache: false,
                    cancellation).ConfigureAwait(false);

                if (!CheckConcurrency(entry, entityLoadResult))
                {
                    // The call to UncheckedLoadEntityAsync has delivered the information 
                    // whether the load result was freshly loaded or not.
                    // We skip loading again if it was already freshly loaded.
                    if (!entityLoadResult.LoadedFromCache)
                    {
                        _logger.LogTrace(
                            Resources.EngineConcurrencyCheckFailed,
                            entry.EntityIdentifier,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);

                        return false;
                    }

                    // The concurrency check failed but this may be due to
                    // - We used a cached version of the entity load result and are behind the actual entry
                    // - We have a concurrency conflict
                    if (entityLoadResult.IsFound())
                    {
                        // As a starter, we check the revision of our entity load result
                        var expectedRevision = entry.ExpectedRevision;

                        // The revision of our entity load result is higher then the expected one.
                        // It does not matter whether we are behind, this is a concurrency failure.
                        if (entityLoadResult.Revision > expectedRevision)
                        {
                            _logger.LogTrace(
                                Resources.EngineConcurrencyCheckFailed,
                                entry.EntityIdentifier,
                                _optionsAccessor.Value.Scope ?? Resources.NoScope);

                            return false;
                        }
                    }

                    _logger.LogTrace(
                        Resources.EngineConcurrencyCheckFailedUpdatingCache,
                        entry.EntityIdentifier,
                        _optionsAccessor.Value.Scope ?? Resources.NoScope);

                    // This is either a failure load result 
                    // or a success load result with a revision smaller then the expected one.
                    // Load again bypassing the cache.
                    entityLoadResult = await UncachedLoadEntityAsync(entry.EntityIdentifier, cancellation)
                        .ConfigureAwait(false);

                    if (!CheckConcurrency(entry, entityLoadResult))
                    {
                        _logger.LogTrace(
                            Resources.EngineConcurrencyCheckFailed,
                            entry.EntityIdentifier,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);

                        return false;
                    }
                }
            }

            return true;
        }

        // TODO: Further simplify and refactor this.       
        //       For example LoadEntityAsync could be implemented directly in the storage engine by calling the 
        //       LoadStoredEntityAsync method. This operates on the IDatabaseScope type other than the LoadEntityAsync 
        //       method. We need a common interface between these to being able to refactor this. 
        //       TODO: Do we have an issue for this?
        //
        //       Move all database accesses to this type and refactor this to some kind of repository for 
        //       stored-entities (like is done for projection-targets in the projection engine)
        private static class StoredEntityHelper
        {
            #region Caching

            private static readonly ConditionalWeakTable<Type, ITypedStoredEntityHelper> _typedStoredEntityHelpers =
                new ConditionalWeakTable<Type, ITypedStoredEntityHelper>();

            private static readonly ConditionalWeakTable<Type, ITypedStoredEntityHelper>.CreateValueCallback _buildTypedStoredEntityHelper =
                BuildTypedStoredEntityHelper;

            private static ITypedStoredEntityHelper GetTypedStoredEntityHelper(Type entityType)
            {
                return _typedStoredEntityHelpers.GetValue(entityType, _buildTypedStoredEntityHelper);
            }

            private static ITypedStoredEntityHelper BuildTypedStoredEntityHelper(Type entityType)
            {
                var helperType = typeof(TypedStoredEntityHelper<>).MakeGenericType(entityType);
                var helper = Activator.CreateInstance(helperType, nonPublic: true) as ITypedStoredEntityHelper;
                Debug.Assert(helper != null);
                return helper!;
            }

            #endregion

            public static ValueTask<(IEntityQueryResult entityQueryResult, int epoch)> LoadEntityAsync(
                Type entityType,
                IDatabase database,
                string? scope,
                string entityId,
                CancellationToken cancellation)
            {
                var typedHelper = GetTypedStoredEntityHelper(entityType);
                return typedHelper.LoadEntityAsync(database, entityId, scope, cancellation);
            }

            public static IAsyncEnumerable<(IFoundEntityQueryResult entityQueryResult, int epoch)> LoadEntitiesAsync(
                Type entityType,
                IDatabase database,
                string? scope,
                CancellationToken cancellation)
            {
                var typedHelper = GetTypedStoredEntityHelper(entityType);
                return typedHelper.LoadEntitiesAsync(database, scope, cancellation);
            }

            public static ValueTask<IStoredEntity?> LoadStoredEntityAsync(
                Type entityType,
                IDatabaseScope databaseScope,
                string? scope,
                string entityId,
                CancellationToken cancellation)
            {
                var typedHelper = GetTypedStoredEntityHelper(entityType);
                return typedHelper.LoadStoredEntityAsync(databaseScope, entityId, scope, cancellation);
            }

            public static ValueTask RemoveStoredEntityAsync(
                IDatabaseScope databaseScope,
                IStoredEntity storedEntity,
                CancellationToken cancellation)
            {
                var typedHelper = GetTypedStoredEntityHelper(storedEntity.EntityType);
                return typedHelper.RemoveStoredEntityAsync(databaseScope, storedEntity, cancellation);
            }

            public static ValueTask StoreStoredEntityAsync(
                IDatabaseScope databaseScope,
                IStoredEntity storedEntity,
                CancellationToken cancellation)
            {
                var typedHelper = GetTypedStoredEntityHelper(storedEntity.EntityType);
                return typedHelper.StoreStoredEntityAsync(databaseScope, storedEntity, cancellation);
            }

            public static IStoredEntity CreateStoredEntity(EntityIdentifier entityIdentifier, string? scope)
            {
                var typedHelper = GetTypedStoredEntityHelper(entityIdentifier.EntityType);
                return typedHelper.CreateStoredEntity(entityIdentifier.EntityId, scope);
            }

            public static Type GetStoredEntityType(Type entityType)
            {
                var typedHelper = GetTypedStoredEntityHelper(entityType);
                return typedHelper.GetStoredEntityType();
            }

            private interface ITypedStoredEntityHelper
            {
                ValueTask<(IEntityQueryResult entityQueryResult, int epoch)> LoadEntityAsync(
                    IDatabase database,
                    string entityId,
                    string? scope,
                    CancellationToken cancellation);

                IAsyncEnumerable<(IFoundEntityQueryResult entityQueryResult, int epoch)> LoadEntitiesAsync(
                    IDatabase database,
                    string? scope,
                    CancellationToken cancellation);

                ValueTask<IStoredEntity?> LoadStoredEntityAsync(
                    IDatabaseScope databaseScope,
                    string entityId,
                    string? scope,
                    CancellationToken cancellation);

                ValueTask RemoveStoredEntityAsync(
                    IDatabaseScope databaseScope,
                    IStoredEntity storedEntity,
                    CancellationToken cancellation);

                ValueTask StoreStoredEntityAsync(
                    IDatabaseScope databaseScope,
                    IStoredEntity storedEntity,
                    CancellationToken cancellation);

                IStoredEntity CreateStoredEntity(string entityId, string? scope);

                Type GetStoredEntityType();
            }

#pragma warning disable CA1812
            private sealed class TypedStoredEntityHelper<TEntity> : ITypedStoredEntityHelper
#pragma warning restore CA1812
                where TEntity : class
            {
                public async ValueTask<(IEntityQueryResult entityQueryResult, int epoch)> LoadEntityAsync(
                    IDatabase database,
                    string entityId,
                    string? scope,
                    CancellationToken cancellation)
                {
                    var storedEntity = await database.GetOneAsync(
                        BuildPredicate(scope, entityId, includeDeleted: false),
                        cancellation).ConfigureAwait(false);

                    var entityIdentifier = new EntityIdentifier(typeof(TEntity), entityId);

                    IEntityQueryResult entityQueryResult;

                    if (storedEntity is null)
                    {
                        entityQueryResult = new NotFoundEntityQueryResult(
                            entityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance);
                    }
                    else
                    {
                        entityQueryResult = new FoundEntityQueryResult(
                            entityIdentifier,
                            storedEntity.Entity!,
                            storedEntity.ConcurrencyToken,
                            storedEntity.Revision,
                            loadedFromCache: false,
                            EntityQueryResultGlobalScope.Instance);
                    }

                    return (entityQueryResult, storedEntity?.Epoch ?? 0);
                }

                private static readonly Func<IStoredEntity, (IFoundEntityQueryResult entityQueryResult, int epoch)> _projectStoredEntityToLoadResult
                    = ProjectStoredEntityToLoadResult;

                private static (IFoundEntityQueryResult entityQueryResult, int epoch) ProjectStoredEntityToLoadResult(IStoredEntity storedEntity)
                {
                    Debug.Assert(!storedEntity.IsMarkedAsDeleted);
                    var entityIdentifier = new EntityIdentifier(typeof(TEntity), storedEntity.EntityId);

                    var entityQueryResult = new FoundEntityQueryResult(
                        entityIdentifier,
                        (TEntity)storedEntity.Entity!,
                        storedEntity.ConcurrencyToken,
                        storedEntity.Revision,
                         loadedFromCache: false,
                        EntityQueryResultGlobalScope.Instance);

                    return (entityQueryResult, storedEntity.Epoch);
                }

                public IAsyncEnumerable<(IFoundEntityQueryResult entityQueryResult, int epoch)> LoadEntitiesAsync(
                    IDatabase database,
                    string? scope,
                    CancellationToken cancellation)
                {
                    return database
                        .GetAsync(BuildPredicate(scope, entityId: null, includeDeleted: false), cancellation)
                        .Select(_projectStoredEntityToLoadResult);
                }

                public async ValueTask<IStoredEntity?> LoadStoredEntityAsync(
                    IDatabaseScope databaseScope,
                    string entityId,
                    string? scope,
                    CancellationToken cancellation)
                {
                    return await databaseScope.GetOneAsync(
                        BuildPredicate(scope, entityId, includeDeleted: true),
                        cancellation).ConfigureAwait(false);
                }

                public ValueTask RemoveStoredEntityAsync(
                    IDatabaseScope databaseScope,
                    IStoredEntity storedEntity,
                    CancellationToken cancellation)
                {
                    var typedStoredEntity = storedEntity as StoredEntity<TEntity>
                        ?? new StoredEntity<TEntity>(storedEntity);

                    return databaseScope.RemoveAsync(typedStoredEntity, cancellation);
                }

                public ValueTask StoreStoredEntityAsync(
                    IDatabaseScope databaseScope,
                    IStoredEntity storedEntity,
                    CancellationToken cancellation)
                {
                    var typedStoredEntity = storedEntity as StoredEntity<TEntity>
                                            ?? new StoredEntity<TEntity>(storedEntity);

                    return databaseScope.StoreAsync(typedStoredEntity, cancellation);
                }

                public IStoredEntity CreateStoredEntity(string entityId, string? scope)
                {
                    return new StoredEntity<TEntity>(entityId, scope);
                }

                public Type GetStoredEntityType()
                {
                    return typeof(StoredEntity<TEntity>);
                }

                private Expression<Func<StoredEntity<TEntity>, bool>> BuildPredicate(
                    string? scope, string? entityId, bool includeDeleted)
                {
                    if (entityId is null)
                    {
                        if (includeDeleted)
                        {
                            if (scope is null)
                            {
                                return _ => true;
                            }

                            return p => p.Scope == scope;
                        }

                        if (scope is null)
                        {
                            return p => !p.IsMarkedAsDeleted;
                        }

                        return p => !p.IsMarkedAsDeleted && p.Scope == scope;

                    }

                    if (includeDeleted)
                    {
                        if (scope is null)
                        {
                            return p => p.EntityId == entityId;
                        }

                        return p => p.EntityId == entityId && p.Scope == scope;
                    }

                    if (scope is null)
                    {
                        return p => !p.IsMarkedAsDeleted && p.EntityId == entityId;
                    }

                    return p => !p.IsMarkedAsDeleted && p.EntityId == entityId && p.Scope == scope;
                }
            }
        }

        private interface IStoredEntity
        {
            string EntityId { get; }
            Type EntityType { get; }

            long Revision { get; set; }
            string ConcurrencyToken { get; set; }

            bool IsMarkedAsDeleted { get; set; }
            int Epoch { get; set; }

            object? Entity { get; set; }

            string? Scope { get; }
        }

        private sealed class StoredEntity<TEntity> : IStoredEntity
            where TEntity : class
        {
            public StoredEntity(string entityId, string? scope)
            {
                EntityId = entityId;
                Scope = scope;
                ConcurrencyToken = string.Empty;

                Id = IdGenerator.GenerateId(EntityId, scope);
            }

            private StoredEntity()
            {
                EntityId = string.Empty;
                ConcurrencyToken = string.Empty;
                Id = string.Empty;
            }

            internal StoredEntity(IStoredEntity copy) : this(copy.EntityId, copy.Scope)
            {
                if (copy.EntityType != typeof(TEntity))
                    throw new ArgumentException();

                Revision = copy.Revision;
                ConcurrencyToken = copy.ConcurrencyToken;
                IsMarkedAsDeleted = copy.IsMarkedAsDeleted;
                Epoch = copy.Epoch;
                Entity = copy.Entity as TEntity;
            }

            public string Id { get; private set; }

            public string EntityId { get; private set; }
            Type IStoredEntity.EntityType => typeof(TEntity);
            public long Revision { get; set; }
            public string ConcurrencyToken { get; set; }
            public bool IsMarkedAsDeleted { get; set; }
            public int Epoch { get; set; }

            public string? Scope { get; private set; }

            // This is non-null, if IsMarkedAsDeleted is false.
            public TEntity? Entity { get; set; }

            object? IStoredEntity.Entity
            {
                get => Entity;
                set
                {
                    if (value is null)
                    {
                        Entity = null;
                    }
                    else if (value is TEntity typedEntity)
                    {
                        Entity = typedEntity;
                    }
                    else
                    {
                        Debug.Assert(false);
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        private sealed class StoredDomainEventBatch
        {
            public static StoredDomainEventBatch Create<TCommitAttemptEntry>(
                TCommitAttemptEntry commitAttemptEntry,
                int entityEpoch,
                string? scope) where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                return new StoredDomainEventBatch
                {
                    EntityDeleted = commitAttemptEntry.Operation == CommitOperation.Delete,
                    EntityType = commitAttemptEntry.EntityIdentifier.EntityType,
                    EntityId = commitAttemptEntry.EntityIdentifier.EntityId,
                    EntityRevision = commitAttemptEntry.Revision,
                    EntityEpoch = entityEpoch,
                    Scope = scope,
                    DomainEvents = CreateStoredDomainEvents(commitAttemptEntry),
                    Id = GenerateId(commitAttemptEntry, entityEpoch, scope)
                };
            }

            private static List<StoredDomainEvent> CreateStoredDomainEvents<TCommitAttemptEntry>(
                TCommitAttemptEntry commitAttemptEntry)
                where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                var result = new List<StoredDomainEvent>(capacity: commitAttemptEntry.DomainEvents.Count);

                // Do not use LINQ as this allocates heavily.
                foreach (var domainEvent in commitAttemptEntry.DomainEvents)
                {
                    result.Add(new StoredDomainEvent(domainEvent));
                }

                return result;
            }

            private static string GenerateId<TCommitAttemptEntry>(
                TCommitAttemptEntry commitAttemptEntry,
                int entityEpoch,
                string? scope) where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                return IdGenerator.GenerateId(
                    commitAttemptEntry.EntityIdentifier.EntityType,
                    commitAttemptEntry.EntityIdentifier.EntityId,
                    commitAttemptEntry.Revision,
                    entityEpoch,
                    scope);
            }

            private StoredDomainEventBatch()
            {
                Id = string.Empty;
                EntityType = typeof(object);
                EntityId = string.Empty;
            }

            public string Id { get; private set; }

            /// <summary>
            /// Gets a boolean value indicating whether the current batch deleted the entity.
            /// </summary>
            /// <remarks>
            /// As the current instance is only created when there are domain-event present for the batch, and this 
            /// returns true, the entity was not deleted but marked as deleted, so the garbage collection procedure
            /// has to take care of actually deleting the entity entry.
            /// </remarks>
            public bool EntityDeleted { get; private set; }

            public Type EntityType { get; private set; }
            public string EntityId { get; private set; }
            public long EntityRevision { get; private set; }
            public int EntityEpoch { get; private set; }

            public string? Scope { get; private set; }

            public List<StoredDomainEvent> DomainEvents { get; private set; } = new List<StoredDomainEvent>();
        }

        private sealed class StoredDomainEvent
        {
            private static readonly object _emptyObject = new object();

            public StoredDomainEvent(DomainEvent domainEvent)
            {
                EventType = domainEvent.EventType;
                Event = domainEvent.Event;
            }

            private StoredDomainEvent()
            {
                EventType = typeof(object);
                Event = _emptyObject;
            }

            public Type EventType { get; private set; }
            public object Event { get; private set; }
        }
    }

    internal sealed class EntityStorageEngineCache
    {
        private readonly object _mutex = new object();
        private readonly Dictionary<EntityIdentifier, (IEntityQueryResult entityQueryResult, int epoch)> _cache;

        public EntityStorageEngineCache()
        {
            _cache = new Dictionary<EntityIdentifier, (IEntityQueryResult entityQueryResult, int epoch)>();
        }

        // As we execute this concurrently, it is actually possible that we "update" the cache with an entry that is
        // older than the version it currently contains. This is OK, as we do not guarantee that the cache contains
        // the latest version anyway.
        // This should be a rare case however and there are some options we have to decrease the probability
        // that this occurs.

        // Check whether the stored-entity epoch is larger then the epoch of the cache entry.
        // -- OR --
        // Check whether the stored-entity epoch is equal then the epoch of the cache entry AND 
        // Check whether the entity revision is larger than the entity revision in the cache.

        // These two checks should sort out most concurrency problems. These is a single situation where we can
        // still override a later version with an older one, that is:
        // An entity is deleted, so that the stored-entity gets deleted. Therefore both, the epoch of 
        // the stored-entity as well as the entity revision is zero. This gets written into the cache.
        // If we have an older version at hand this will have a higher revision (and possibly epoch) and will
        // therefor override the later version. This also occurs when we re-create the entity in the mean-time, as
        // long as the entity revision is still smaller than the one of the old version (as well as the epoch).

        // There are possibly multiple solutions to this problem:
        // 1. We never actually never delete a stored-entity.
        // 2. We synchronize all EntityStorageEngine instances to remove the stored-entity from the cache before
        //    removing it.
        // 3. We could replace the epoch with a unique identifier so that an epoch is never used twice.
        // 3.1 This unique identifier can be a time-stamp (with an additional counter when multiple identifiers are 
        //     created in the same tick). This has to be combined with a unique "creator-id" so that it is not 
        //     possible any more that multiple creators create the same unique-id. This has to be done because
        //     the clocks at each creator may differ in time by some extend. 
        // 3.2 Alternatively we could combine the epoch version with the unique-identifier version, so that we 
        //     do not need unique-creator ids any more. The general idea is the following: When we create a unique-
        //     identifier (via a time-stamp and a counter, like in 3.1) we check in the database whether the 
        //     identifier is still available. So we have to keep track of all identifiers that are already taken.
        //     We now have the same problem that we cannot delete these entries. But we now that the identifiers
        //     are based on time-stamps. As a solution we could split the identifier-tracking in buckets, so that 
        //     we open a new bucket whenever a certain amount of time elapsed. When we open up a new bucket we can
        //     delete all entries of the old one. If a creator now wants to create an identifier, it can easily 
        //     check via the time-stamp of the bucket whether its clock is off and possible store the difference
        //     and use this information to create a valid unique-identifier.

        // As all of these solutions are relatively heavy weights and the benefits may actually not be worth the 
        // costs we have to benchmark this. Therefore none if this is implemented currently.

        public bool UpdateCache(IEntityQueryResult entityQueryResult, int epoch)
        {
            entityQueryResult = entityQueryResult.AsCachedResult();
            bool updateCache;

            lock (_mutex)
            {
                updateCache = UpdateCache(entityQueryResult, epoch, out var entry);

                if (updateCache)
                {
                    _cache[entityQueryResult.EntityIdentifier] = (entityQueryResult, epoch);
                }
                else
                {
                    entityQueryResult = entry.entityQueryResult;
                }
            }

            return updateCache;
        }

        private bool UpdateCache(
            IEntityQueryResult entityQueryResult,
            int epoch,
            out (IEntityQueryResult entityQueryResult, int epoch) entry)
        {
            entry = default;
            return true;

            //if (!_cache.TryGetValue(entityQueryResult.EntityIdentifier, out entry))
            //    return true;

            //if (epoch > entry.epoch)
            //    return true;

            //if (epoch == entry.epoch && entityQueryResult.Revision > entry.entityQueryResult.Revision)
            //    return true;

            //return false;
        }

        public bool TryGetFromCache(
            EntityIdentifier entityIdentifier,
            [NotNullWhen(true)] out IEntityQueryResult? entityQueryResult)
        {
            (IEntityQueryResult entityQueryResult, int epoch) entry = default;
            bool result;

            lock (_mutex)
            {
                result = _cache.TryGetValue(entityIdentifier, out entry);
            }

            entityQueryResult = entry.entityQueryResult;
            return result;
        }
    }
}
