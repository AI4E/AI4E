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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage.Domain.Tracking;
using AI4E.Utils;
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
    public sealed partial class EntityStorage : IEntityStorage
    {
        private readonly IEntityStorageEngine _storageEngine;
        private readonly IEntityIdFactory _idFactory;
        private readonly IOptions<DomainStorageOptions> _optionsAccessor;
        private readonly ILogger<EntityStorage> _logger;

        private readonly IEntityQueryResultScope _queryResultScope = new EntityQueryResultScope(); // TODO: We could pool this  
        private readonly IUnitOfWork<IEntityQueryResult> _unitOfWork;
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

            // TODO: We could pool these        
            _domainQueryExecutor = new DomainQueryExecutor(this);
            _unitOfWork = new UnitOfWork<IEntityQueryResult>(concurrencyTokenFactory);
        }

        /// <inheritdoc/>
        public IEnumerable<IFoundEntityQueryResult> LoadedEntities
        {
            get
            {
                var entries = _unitOfWork.Entries;
                var result = ImmutableList.CreateBuilder<IFoundEntityQueryResult>();

                // Using a for loop because of perf here
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var entityLoadResult = entry.EntityLoadResult;

                    if (entityLoadResult.IsFound(out var foundEntityQueryResult))
                    {
                        result.Add(foundEntityQueryResult);
                    }
                }

                return result.ToImmutableList();
            }
        }

        /// <inheritdoc/>
        public IEntityMetadataManager MetadataManager { get; }

        /// <inheritdoc/>
        public async IAsyncEnumerable<IFoundEntityQueryResult> LoadEntitiesAsync(
            Type entityType,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            EntityValidationHelper.Validate(entityType);

            _logger.LogInformation(
                Resources.LoadingEntities,
                _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var loadResults = _storageEngine.QueryEntitiesAsync(
                entityType, bypassCache: false, cancellation);

            // TODO: If multiple iterators overlap, we may get some concurrency here.
            //       Do we have to synchronize and if yes, what are the critical sections?
            await foreach (var iteratedLoadResult in loadResults.WithCancellation(cancellation))
            {
                _logger.LogTrace(
                    Resources.ProcessingEntity,
                    iteratedLoadResult.EntityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                var entityLoadResult = iteratedLoadResult.AsScopedTo(_queryResultScope).ToQueryResult();
                var entry = _unitOfWork.GetOrUpdate(entityLoadResult);

                if (!entry.EntityLoadResult.IsFound(out entityLoadResult))
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

                return new ValueTask<IEntityLoadResult>(new NotFoundEntityQueryResult(
                    entityIdentifier, loadedFromCache: false, scope: _queryResultScope));
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

            entityLoadResult = entityLoadResult.AsScopedTo(_queryResultScope);

            if (entityLoadResult.GetEntity(throwOnFailure: false) != null)
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

            if (entityLoadResult.IsTrackable<IEntityQueryResult>(out var trackableEntityLoadResult))
            {
                _logger.LogTrace(
                    Resources.UpdatingUnitOfWork,
                    entityIdentifier,
                    _optionsAccessor.Value.Scope ?? Resources.NoScope);

                var entry = _unitOfWork.GetOrUpdate(trackableEntityLoadResult);

                // TODO: Check whether we actually updated the cache? Is it possible that we could not update the cache
                //       if we assume that the storage is not used in a concurrent manner?
            }

            return entityLoadResult;
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

            var trackedEntity = await LoadUnitOfWorkEntryAsync(
                new EntityIdentifier(entityDescriptor.EntityType, entityId), cancellation)
                .ConfigureAwait(false);

            var domainEvents = MetadataManager.GetUncommittedEvents(entityDescriptor);
            trackedEntity = trackedEntity.CreateOrUpdate(entityDescriptor.Entity, domainEvents);

            SetEntityMetadata(
                entityDescriptor,
                entityId,
                trackedEntity.EntityLoadResult.ConcurrencyToken,
                trackedEntity.EntityLoadResult.Revision);
        }

        /// <inheritdoc/>
        public async ValueTask DeleteAsync(EntityDescriptor entityDescriptor, CancellationToken cancellation)
        {
            var entityId = GetOrCreateId(entityDescriptor);

            _logger.LogDebug(
                  Resources.Deleting,
                  new EntityIdentifier(entityDescriptor.EntityType, entityId),
                  _optionsAccessor.Value.Scope ?? Resources.NoScope);

            var entry = await LoadUnitOfWorkEntryAsync(
                new EntityIdentifier(entityDescriptor.EntityType, entityId), cancellation)
                .ConfigureAwait(false);

            var domainEvents = MetadataManager.GetUncommittedEvents(entityDescriptor);
            var modifiedEntry = entry.Delete(domainEvents);

            // TODO: Do we set default values? When does this occur?
            var concurrencyToken = default(ConcurrencyToken);
            var revision = 0L;

            if (modifiedEntry != null)
            {
                concurrencyToken = modifiedEntry.EntityLoadResult.ConcurrencyToken;
                revision = modifiedEntry.EntityLoadResult.Revision;
            }

            SetEntityMetadata(entityDescriptor, entityId, concurrencyToken, revision);
        }

        private ValueTask<IUnitOfWorkEntry<IEntityQueryResult>> LoadUnitOfWorkEntryAsync(
            EntityIdentifier entityIdentifier,
            CancellationToken cancellation)
        {
            if (_unitOfWork.TryGetEntry(entityIdentifier, out var entry))
            {
                return new ValueTask<IUnitOfWorkEntry<IEntityQueryResult>>(entry);
            }

            return UncachedLoadUnitOfWorkEntryAsync(entityIdentifier, cancellation);
        }

        private async ValueTask<IUnitOfWorkEntry<IEntityQueryResult>> UncachedLoadUnitOfWorkEntryAsync(
            EntityIdentifier entityIdentifier,
            CancellationToken cancellation)
        {
            var entityLoadResult = await _storageEngine.QueryEntityAsync(
                entityIdentifier,
                bypassCache: false,
                cancellation).ConfigureAwait(false);

            entityLoadResult = entityLoadResult.AsScopedTo(_queryResultScope);

            return _unitOfWork.GetOrUpdate(entityLoadResult);
        }

        private void SetEntityMetadata(IEntityLoadResult entityLoadResult)
        {
            var entity = entityLoadResult.GetEntity(throwOnFailure: false);

            if (entity is null)
                return;

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

    partial class EntityStorage
    {
        private sealed class DomainQueryExecutor : IDomainQueryExecutor
        {
            private readonly EntityStorage _entityStorage;

            public DomainQueryExecutor(EntityStorage entityStorage)
            {
                _entityStorage = entityStorage;
            }

            public async ValueTask<IEntityQueryResult> ExecuteAsync(
                EntityIdentifier entityIdentifier,
                bool bypassCache,
                CancellationToken cancellation = default)
            {
                _entityStorage._logger.LogTrace(
                    Resources.InvokingQueryExecutor,
                    entityIdentifier,
                    _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);

                IEntityQueryResult entityLoadResult;

                // We MUST never bypass our cache (uow) only the low level caching of the storage engine.
                // See the guarantees on concurrent access for further details.
                if (_entityStorage._unitOfWork.TryGetEntry(entityIdentifier, out var entry))
                {
                    _entityStorage._logger.LogTrace(
                        Resources.QueryExecutorLoadingEntityFromUnitOfWork,
                        entityIdentifier,
                        _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);

                    entityLoadResult = entry.EntityLoadResult.ApplyRecordedOperations();
                }
                else
                {
                    _entityStorage._logger.LogTrace(
                        Resources.QueryExecutorLoadingEntityFromStorageEngine,
                        entityIdentifier,
                         _entityStorage._optionsAccessor.Value.Scope ?? Resources.NoScope);

                    entityLoadResult = await _entityStorage._storageEngine.QueryEntityAsync(
                        entityIdentifier,
                        bypassCache,
                        cancellation).ConfigureAwait(false);

                    entityLoadResult = entityLoadResult.AsScopedTo(_entityStorage._queryResultScope);
                }

                if (entityLoadResult.IsFound())
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
    }
}
