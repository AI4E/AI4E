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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// TODO: Document the difference in concurrency strategies in
//       (1) Offline: The entity is loaded (potentially projected), sent to the client, the updated entity is commit back via (2)
//       (2) Online: When committing updates on an entity, the entity is freshly loaded, offline-concurrency is checked and then this is committed back with an online concurrency check.

// TODO: What (offline-) concurrency guaranteed do we provide?
//       Lost update prevention: YES (via concurrency-tokens)
//       Dirty-read, Non-repeatable read, Phantom-read : (The same as in the online read. When reading the is no round-trip to the client. This roundtrip is done AFTER reading is complete. )        

// TODO: What (online-)concurrency guarantees do we provide?
//       Lost update prevention: YES (this is done via checking the entity revision)
//       Dirty-read              YES (this is provided by the underlying storage-engine, with native db-transactions in the default case)
//       Non-repeatable read     YES (the built in scoped entity cache/tracker is responsible for this)
//       Phantom-read            MAYBE // TODO

// TODO: In the case of querying all entities of a given type or when we add querying via predicated, 'Non-repeatable read' is currently not guaranteed, as entities may have been added or removed in the underlying storage engine. We have to compensate for this.

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityStorage"/>
    public sealed class EntityStorage : IEntityStorage
    {
        private readonly IEntityStorageEngine _storageEngine;
        private readonly IEntityIdFactory _idFactory;
        private readonly IOptions<DomainStorageOptions> _optionsAccessor;
        private readonly ILogger<EntityStorage> _logger;

        private readonly IUnitOfWork _unitOfWork;
        private readonly IDomainQueryExecutor _domainQueryExecutor;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityStorage"/> type.
        /// </summary>
        /// <param name="storageEngine">The underlying domain storage-engine.</param>
        /// <param name="metadataManager">
        /// The metadata manager that is used to get and set entity meta-data to entities.
        /// </param>
        /// <param name="idFactory">The entity id factory that is used to create entity ids.</param>
        /// <param name="concurrencyTokenFactory">
        /// The concurrency-token factory that is used to create concurrency-tokens.
        /// </param>
        /// <param name="optionsAccessor">
        /// An <see cref="IOptions{DomainStorageOptions}"/> that is used to resolve domain storage options.
        /// </param>
        /// <param name="logger">
        /// The <see cref="ILogger{EntityStorage}"/> used for logging or <c>null</c> to disable logging.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="storageEngine"/>, <paramref name="metadataManager"/>,
        /// <paramref name="idFactory"/>, <paramref name="concurrencyTokenFactory"/> 
        /// or <paramref name="optionsAccessor"/> is <c>null</c>.
        /// </exception>
        public EntityStorage(
            IEntityStorageEngine storageEngine,
            IEntityMetadataManager metadataManager,
            IEntityIdFactory idFactory,
            IEntityConcurrencyTokenFactory concurrencyTokenFactory,
            IOptions<DomainStorageOptions> optionsAccessor,
            ILogger<EntityStorage>? logger = null)
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (metadataManager is null)
                throw new ArgumentNullException(nameof(metadataManager));

            if (idFactory is null)
                throw new ArgumentNullException(nameof(idFactory));

            if (concurrencyTokenFactory is null)
                throw new ArgumentNullException(nameof(concurrencyTokenFactory));

            if (optionsAccessor is null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _storageEngine = storageEngine;
            MetadataManager = metadataManager;
            _idFactory = idFactory;
            _optionsAccessor = optionsAccessor;
            _logger = logger ?? NullLogger<EntityStorage>.Instance;

            _unitOfWork = new UnitOfWork(concurrencyTokenFactory);
            _domainQueryExecutor = new DomainQueryExecutor(this);
        }

        /// <inheritdoc/>
        public IEnumerable<ISuccessEntityLoadResult> LoadedEntities
        {
            get
            {
                // TODO: Perf
                return _unitOfWork
                    .TrackedEntities
                    .Where(p => p.EntityLoadResult.IsSuccess())
                    .Select(p => p.EntityLoadResult.AsSuccessLoadResult())
                    .ToImmutableList();
            }
        }

        /// <inheritdoc/>
        public IEntityMetadataManager MetadataManager { get; }

        /// <inheritdoc/>
        public async IAsyncEnumerable<ISuccessEntityLoadResult> LoadEntitiesAsync(
            Type entityType,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            // TODO: Validate entity type

            _logger.LogInformation(
                Resources.LoadingEntities,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var loadResults = _storageEngine.LoadEntitiesAsync(entityType, bypassCache: false, cancellation);

            // TODO: If multiple iterators overlap, we may get some concurrency here.
            //       Do we have to synchronize and if yes, what are the critical sections?
            await foreach (var iteratedLoadResult in loadResults)
            {
                _logger.LogTrace(
                    Resources.ProcessingEntity,
                    iteratedLoadResult.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                var entityLoadResult = iteratedLoadResult.ScopeTo(this);
                var trackedEntity = _unitOfWork.GetOrUpdate(entityLoadResult);

                if (!trackedEntity.EntityLoadResult.IsSuccess())
                {
                    _logger.LogTrace(
                    Resources.UpdatedTrackedEntityInUnitOfWorkNonExisting,
                    iteratedLoadResult.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                    continue;
                }

                _logger.LogTrace(
                    Resources.UpdatedTrackedEntityInUnitOfWork,
                    iteratedLoadResult.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                entityLoadResult = trackedEntity.EntityLoadResult.AsSuccessLoadResult();

                SetEntityMetadata(
                    new EntityDescriptor(entityType, entityLoadResult.Entity),
                    entityLoadResult.EntityIdentifier.EntityId,
                    entityLoadResult.ConcurrencyToken,
                    entityLoadResult.Revision);

                yield return entityLoadResult;
            }

            _logger.LogDebug(
                Resources.LoadedEntities,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);
        }

        /// <inheritdoc/>
        public ValueTask<IEntityLoadResult> LoadEntityAsync(
            EntityIdentifier entityIdentifier,
            IDomainQueryProcessor queryProcessor,
            CancellationToken cancellation)
        {
            if (queryProcessor is null)
                throw new ArgumentNullException(nameof(queryProcessor));

            if (entityIdentifier == default)
            {
                _logger.LogWarning(
                    Resources.LoadingDefaultEntityIdentifier, _optionsAccessor.Value.Scope ?? Resources.NoScope);

                return new ValueTask<IEntityLoadResult>(new NotFoundEntityLoadResult(entityIdentifier));
            }

            return UncheckedLoadEntityAsync(entityIdentifier, queryProcessor, cancellation);
        }

        private async ValueTask<IEntityLoadResult> UncheckedLoadEntityAsync(
            EntityIdentifier entityIdentifier,
            IDomainQueryProcessor queryProcessor,
            CancellationToken cancellation)
        {
            _logger.LogTrace(
               Resources.RequestedEntity,
               entityIdentifier,
               queryProcessor.GetType().GetUnqualifiedTypeName(),
               _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var entityLoadResult = await queryProcessor.ProcessAsync(
                entityIdentifier, _domainQueryExecutor, cancellation).ConfigureAwait(false);

            if (entityLoadResult.IsSuccess())
            {
                _logger.LogInformation(
                    Resources.LoadedEntity,
                    entityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);
            }
            else
            {
                _logger.LogInformation(
                    Resources.FailureLoadingEntity,
                    entityIdentifier,
                    entityLoadResult.Reason ?? Resources.UnknownReason,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);
            }

            if (entityLoadResult is ICacheableEntityLoadResult cacheableLoadResult)
            {
                _logger.LogTrace(
                    Resources.UpdatingUnitOfWork,
                    entityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                var trackedEntity = _unitOfWork.GetOrUpdate(cacheableLoadResult);

                // TODO: Check whether we actually updated the cache? Is it possible that we could not update the cache
                //       if we assume that the storage is not used in a concurrent manner?
            }

            return entityLoadResult;
        }

        private sealed class DomainQueryExecutor : IDomainQueryExecutor
        {
            private readonly EntityStorage _entityStorage;

            public DomainQueryExecutor(EntityStorage entityStorage)
            {
                _entityStorage = entityStorage;
            }

            public async ValueTask<ICacheableEntityLoadResult> ExecuteAsync(
                EntityIdentifier entityIdentifier,
                bool bypassCache,
                CancellationToken cancellation = default)
            {
                _entityStorage._logger.LogTrace(
                    Resources.InvokingQueryExecutor,
                    entityIdentifier,
                    _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);

                ICacheableEntityLoadResult entityLoadResult;

                // We MUST never bypass our cache (uow) only the low level caching of the storage engine.
                // See the guarantees on concurrent access for further details.
                if (_entityStorage._unitOfWork.TryGetTrackedEntity(entityIdentifier, out var trackedEntity))
                {
                    _entityStorage._logger.LogTrace(
                        Resources.QueryExecutorLoadingEntityFromUnitOfWork,
                        entityIdentifier,
                        _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);

                    entityLoadResult = trackedEntity.EntityLoadResult;
                }
                else
                {
                    _entityStorage._logger.LogTrace(
                        Resources.QueryExecutorLoadingEntityFromStorageEngine,
                        entityIdentifier,
                         _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);

                    entityLoadResult = await _entityStorage._storageEngine.LoadEntityAsync(
                        entityIdentifier,
                        bypassCache,
                        cancellation).ConfigureAwait(false);

                    if (entityLoadResult is IScopeableEnityLoadResult scopeableEnityLoadResult)
                    {
                        entityLoadResult = (ICacheableEntityLoadResult)scopeableEnityLoadResult.ScopeTo(_entityStorage);
                    }
                }

                if (entityLoadResult.IsSuccess())
                {
                    _entityStorage._logger.LogDebug(
                        Resources.QueryExecutorLoadedEntity,
                        entityIdentifier,
                        _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);
                }
                else
                {
                    _entityStorage._logger.LogDebug(
                        Resources.QueryExecutorFailureLoadingEntity,
                        entityIdentifier,
                        entityLoadResult.Reason ?? Resources.UnknownReason,
                        _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);
                }

                _entityStorage.SetEntityMetadata(entityLoadResult);
                return entityLoadResult;
            }
        }

        private string GetOrCreateId(EntityDescriptor entityDescriptor)
        {
            var id = MetadataManager.GetId(entityDescriptor);

            if (id is null)
            {
                id = _idFactory.CreateId(entityDescriptor);
                MetadataManager.SetId(entityDescriptor, id);
            }

            return id;
        }

        /// <inheritdoc/>
        public async ValueTask StoreAsync(EntityDescriptor entityDescriptor, CancellationToken cancellation)
        {
            var entityId = GetOrCreateId(entityDescriptor);

            _logger.LogDebug(
                 Resources.Storing,
                 new EntityIdentifier(entityDescriptor.EntityType, entityId),
                 _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var trackedEntity = await LoadTrackedEntityAsync(
                new EntityIdentifier(entityDescriptor.EntityType, entityId), cancellation)
                .ConfigureAwait(false);

            var domainEvents = MetadataManager.GetUncommittedEvents(entityDescriptor);
            trackedEntity = trackedEntity.CreateOrUpdate(entityDescriptor.Entity, domainEvents);

            SetEntityMetadata(entityDescriptor, entityId, trackedEntity.ConcurrencyToken, trackedEntity.Revision);
        }

        /// <inheritdoc/>
        public async ValueTask DeleteAsync(EntityDescriptor entityDescriptor, CancellationToken cancellation)
        {
            var entityId = GetOrCreateId(entityDescriptor);

            _logger.LogDebug(
                  Resources.Deleting, 
                  new EntityIdentifier(entityDescriptor.EntityType, entityId),
                  _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var trackedEntity = await LoadTrackedEntityAsync(
                new EntityIdentifier(entityDescriptor.EntityType, entityId), cancellation)
                .ConfigureAwait(false);

            var domainEvents = MetadataManager.GetUncommittedEvents(entityDescriptor);
            var modifiedTrackedEntity = trackedEntity.Delete(domainEvents);

            // TODO: Do we set default values? When does this occur?
            var concurrencyToken = default(ConcurrencyToken);
            var revision = 0L;

            if (modifiedTrackedEntity != null)
            {
                concurrencyToken = modifiedTrackedEntity.ConcurrencyToken;
                revision = modifiedTrackedEntity.Revision;
            }

            SetEntityMetadata(entityDescriptor, entityId, concurrencyToken, revision);
        }

        private ValueTask<ITrackedEntity> LoadTrackedEntityAsync(
            EntityIdentifier entityIdentifier,
            CancellationToken cancellation)
        {
            if (_unitOfWork.TryGetTrackedEntity(entityIdentifier, out var trackedEntity))
            {
                return new ValueTask<ITrackedEntity>(trackedEntity);
            }

            return UncachedLoadTrackedEntityAsync(entityIdentifier, cancellation);
        }

        private async ValueTask<ITrackedEntity> UncachedLoadTrackedEntityAsync(
            EntityIdentifier entityIdentifier,
            CancellationToken cancellation)
        {
            var entityLoadResult = await _storageEngine.LoadEntityAsync(
                entityIdentifier,
                bypassCache: false,
                cancellation).ConfigureAwait(false);

            if (entityLoadResult is IScopeableEnityLoadResult scopeableEnityLoadResult)
            {
                entityLoadResult = (ICacheableEntityLoadResult)scopeableEnityLoadResult.ScopeTo(this);
            }

            return _unitOfWork.GetOrUpdate(entityLoadResult);
        }

        private void SetEntityMetadata(IEntityLoadResult entityLoadResult)
        {
            if (!entityLoadResult.IsSuccess(out var entity))
            {
                return;
            }

            var entityDescriptor = new EntityDescriptor(entityLoadResult.EntityIdentifier.EntityType, entity);

            SetEntityMetadata(
                entityDescriptor,
                entityLoadResult.EntityIdentifier.EntityId,
                entityLoadResult.ConcurrencyToken,
                entityLoadResult.Revision);
        }

        private void SetEntityMetadata(
            EntityDescriptor entityDescriptor, string entityId, ConcurrencyToken concurrencyToken, long revision)
        {
            _logger.LogTrace(
                Resources.WritingMetadataToEntity,
                new EntityIdentifier(entityDescriptor.EntityType, entityId),
                concurrencyToken,
                revision,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            MetadataManager.SetId(entityDescriptor, entityId);
            MetadataManager.SetConcurrencyToken(entityDescriptor, concurrencyToken);
            MetadataManager.SetRevision(entityDescriptor, revision);
            MetadataManager.CommitEvents(entityDescriptor);
        }

        /// <inheritdoc/>
        public ValueTask RollbackAsync(CancellationToken cancellation)
        {
            _logger.LogTrace(
                Resources.RollingBack, _optionsAccessor.Value.Scope ?? Resources.NoScope);

            _unitOfWork.Reset();
            return default;
        }

        /// <inheritdoc/>
        public ValueTask<EntityCommitResult> CommitAsync(CancellationToken cancellation)
        {
            _logger.LogDebug(
                Resources.Committing, _optionsAccessor.Value.Scope ?? Resources.NoScope);

            return _unitOfWork.CommitAsync(_storageEngine, cancellation);
        }

        /// <inheritdoc/>
        public void Dispose() { }
    }
}
