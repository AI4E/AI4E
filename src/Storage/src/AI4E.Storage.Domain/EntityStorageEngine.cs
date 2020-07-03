/* License
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// TODO: Register as application service.
// TODO: Implement IDisposable and cancel initialization 
// TODO: Await initialization

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityStorageEngine"/>
    public sealed class EntityStorageEngine : IEntityStorageEngine
    {
        private readonly IDatabase _database;
        private readonly IDomainEventDispatcher _eventDispatcher;
        private readonly IOptions<DomainStorageOptions> _optionsAccessor;
        private readonly ILogger<EntityStorageEngine> _logger;

        private readonly ConcurrentDictionary<EntityIdentifier, IEntityQueryResult> _entities;
        private readonly AsyncInitializationHelper _initHelper;

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

            _entities = new ConcurrentDictionary<EntityIdentifier, IEntityQueryResult>();
            _initHelper = new AsyncInitializationHelper(InitInternalAsync);
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

            if (!batch.EntityDeleted) // TODO: Is the negation wrong?
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

        private Task Initialization => _initHelper.Initialization;

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

            // Load all undispatched domain event batches
            var batches = _database.GetAsync(BuildPredicate(_optionsAccessor.Value.Scope), cancellation);

            await foreach (var eventBatch in batches)
            {
                _ = RegisterForDispatchAsync(eventBatch, cancellation);
            }

            _logger.LogTrace(
              Resources.EngineInitialized,
              _optionsAccessor.Value.Scope ?? Resources.NoScope);
        }

        #endregion

        /// <inheritdoc/>
        public ValueTask<IEntityQueryResult> QueryEntityAsync(
            EntityIdentifier entityIdentifier,
            bool bypassCache,
            CancellationToken cancellation)
        {
            if (entityIdentifier == default)
            {
                _logger.LogWarning(
                    Resources.EngineLoadingDefaultEntityIdentifier, _optionsAccessor.Value.Scope ?? Resources.NoScope);

                return new ValueTask<IEntityQueryResult>(new NotFoundEntityQueryResult(
                    entityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance));
            }

            return UncheckedLoadEntityAsync(entityIdentifier, bypassCache, cancellation);
        }

        private ValueTask<IEntityQueryResult> UncheckedLoadEntityAsync(
            EntityIdentifier entityIdentifier,
            bool bypassCache,
            CancellationToken cancellation)
        {
            if (!bypassCache
                && _entities.TryGetValue(entityIdentifier, out var entityLoadResult))
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


            var entityLoadResult = await StoredEntityHelper.LoadEntityAsync(
                entityIdentifier.EntityType,
                _database,
                _optionsAccessor.Value.Scope,
                entityIdentifier.EntityId,
                cancellation).ConfigureAwait(false);

            // TODO: Other than in the Entity storage we want the latest entity in the cache.
            //       So we always have to update the cache.
            //       As we execute this concurrently, how can we check whether the cache entry has a higher revision 
            //       if we can store failure load result that by definition do not have revisions?
            _entities.GetOrAdd(entityIdentifier, entityLoadResult.AsCachedResult());

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

            _logger.LogDebug(
                Resources.EngineLoadingEntitiesFromDatabase,
                entityType,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var entityLoadResults = StoredEntityHelper.LoadEntitiesAsync(
                entityType, _database, _optionsAccessor.Value.Scope, cancellation);

            await foreach (var entityLoadResult in entityLoadResults.WithCancellation(cancellation))
            {
                _logger.LogTrace(
                    Resources.EngineProcessingEntity,
                    entityLoadResult.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                // TODO: Other than in the Entity storage we want the latest entity in the cache.
                //       So we always have to update the cache.
                //       As we execute this concurrently, how can we check whether the cache entry has a higher revision 
                //       if we can store failure load result that by definition do not have revisions?
                _entities.GetOrAdd(entityLoadResult.EntityIdentifier, entityLoadResult.AsCachedResult());

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

            using var databaseScope = _database.CreateScope();
            bool concurrencyFailure;
            var eventBatches = new List<StoredDomainEventBatch>();
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
                        Resources.EngineStartedCommitTransaction, _optionsAccessor.Value.Scope ?? Resources.NoScope);
                }
                else
                {
                    _logger.LogTrace(
                        Resources.EngineFailedToCommitTransaction, _optionsAccessor.Value.Scope ?? Resources.NoScope);
                }

                concurrencyFailure = false;
                eventBatches.Clear();

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

                    if (storedEntityRevision != entry.ExpectedRevision)
                    {
                        concurrencyFailure = true;
                        await databaseScope.RollbackAsync(cancellation).ConfigureAwait(false);
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

                    // TODO: Do we allow a deleted entity to be deleted? What to do with the domain-events in this case?

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
                    }
                    else
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
                    }

                    if (!concurrencyFailure && entry.DomainEvents.Count != 0)
                    {
                        _logger.LogTrace(
                            Resources.EngineWritingDomainEventBatchToDatabase,
                            entry.EntityIdentifier,
                            _optionsAccessor.Value.Scope ?? Resources.NoScope);

                        var eventBatch = StoredDomainEventBatch.Create(
                            entry, storedEntity!.Epoch, _optionsAccessor.Value.Scope);
                        eventBatches.Add(eventBatch);
                        await databaseScope.StoreAsync(eventBatch, cancellation).ConfigureAwait(false);
                    }
                }
            }
            while (concurrencyFailure || !await databaseScope.TryCommitAsync(cancellation).ConfigureAwait(false));

            _logger.LogDebug(
                Resources.EngineCommitSuccess,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            // Add event-batches to dispatch queue
            // TODO: Do we allow cancellation here? The entity is already committed.
            //       (YES, but do not cancel the underlying operation, just ignore the operation result; fire & forget)
            var task = RegisterForDispatchAsync(eventBatches, cancellation);

            if (_optionsAccessor.Value.WaitForEventsDispatch)
            {
                await task.ConfigureAwait(false);
            }

            return EntityCommitResult.Success;
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

        // TODO: If the entry in the cache is of another epoch as the one in the database but with the same revision,
        //       the concurrency-check succeeds, although it has to fail. This will blow up when trying to commit the 
        //       transaction to the database, performing the check on the cache again, leading to an infinite loop.
        private async ValueTask<bool> CheckConcurrencyAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            foreach (var entry in commitAttempt.Entries)
            {
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

            public static ValueTask<IEntityQueryResult> LoadEntityAsync(
                Type entityType,
                IDatabase database,
                string? scope,
                string entityId,
                CancellationToken cancellation)
            {
                var typedHelper = GetTypedStoredEntityHelper(entityType);
                return typedHelper.LoadEntityAsync(database, entityId, scope, cancellation);
            }

            public static IAsyncEnumerable<IFoundEntityQueryResult> LoadEntitiesAsync(
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
                ValueTask<IEntityQueryResult> LoadEntityAsync(
                    IDatabase database,
                    string entityId,
                    string? scope,
                    CancellationToken cancellation);

                IAsyncEnumerable<IFoundEntityQueryResult> LoadEntitiesAsync(
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
                public async ValueTask<IEntityQueryResult> LoadEntityAsync(
                    IDatabase database,
                    string entityId,
                    string? scope,
                    CancellationToken cancellation)
                {
                    var storedEntity = await database.GetOneAsync(
                        BuildPredicate(scope, entityId, includeDeleted: false),
                        cancellation).ConfigureAwait(false);

                    var entityIdentifier = new EntityIdentifier(typeof(TEntity), entityId);

                    if (storedEntity is null)
                    {
                        return new NotFoundEntityQueryResult(
                            entityIdentifier, loadedFromCache: false, EntityQueryResultGlobalScope.Instance);
                    }

                    return new FoundEntityQueryResult(
                        entityIdentifier, 
                        storedEntity.Entity!, 
                        storedEntity.ConcurrencyToken, 
                        storedEntity.Revision, 
                        loadedFromCache: false, 
                        EntityQueryResultGlobalScope.Instance);
                }

                private static readonly Func<IStoredEntity, IFoundEntityQueryResult> _projectStoredEntityToLoadResult
                    = ProjectStoredEntityToLoadResult;

                private static IFoundEntityQueryResult ProjectStoredEntityToLoadResult(IStoredEntity storedEntity)
                {
                    Debug.Assert(!storedEntity.IsMarkedAsDeleted);
                    var entityIdentifier = new EntityIdentifier(typeof(TEntity), storedEntity.EntityId);

                    return new FoundEntityQueryResult(
                        entityIdentifier,
                        (TEntity)storedEntity.Entity!,
                        storedEntity.ConcurrencyToken,
                        storedEntity.Revision,
                         loadedFromCache: false,
                        EntityQueryResultGlobalScope.Instance);
                }

                public IAsyncEnumerable<IFoundEntityQueryResult> LoadEntitiesAsync(
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
                    if (value is TEntity typedEntity)
                    {
                        Entity = typedEntity;
                    }

                    Debug.Assert(false);
                    throw new InvalidOperationException();
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
}
