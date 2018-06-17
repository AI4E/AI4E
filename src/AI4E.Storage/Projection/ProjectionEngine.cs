using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Domain;
using AI4E.Storage.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Projection
{
    public sealed class ProjectionEngine : IProjectionEngine
    {
        private readonly IProjector _projector;
        private readonly IProvider<ITransactionalDatabase> _databaseFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionEngine> _logger;

        public ProjectionEngine(IProjector projector,
                                IProvider<ITransactionalDatabase> databaseFactory,
                                IServiceProvider serviceProvider,
                                ILogger<ProjectionEngine> logger = default)
        {
            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (databaseFactory == null)
                throw new ArgumentNullException(nameof(databaseFactory));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _projector = projector;
            _databaseFactory = databaseFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task ProjectAsync(Type entityType, string id, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            var processedEntities = new HashSet<EntityDescriptor>();
            return ProjectSingleAsync(new EntityDescriptor(entityType, id), processedEntities, cancellation);
        }

        private async Task ProjectSingleAsync(EntityDescriptor entityDescriptor,
                                              ISet<EntityDescriptor> processedEntities,
                                              CancellationToken cancellation)
        {
            if (processedEntities.Contains(entityDescriptor))
            {
                return;
            }

            IEnumerable<EntityDescriptor> dependents;

            ITransactionalDatabase database = null;
            try
            {
                do
                {
                    await database?.DisposeIfDisposableAsync();
                    database = _databaseFactory.ProvideInstance();
                    var scopedEngine = new ScopedProjectionEngine(entityDescriptor, _projector, database, _serviceProvider);
                    dependents = await scopedEngine.ProjectAsync(cancellation);
                }
                while (!await database.TryCommitAsync(cancellation));
            }
            finally
            {
                await database?.DisposeIfDisposableAsync();
            }

            processedEntities.Add(entityDescriptor);

            foreach (var dependent in dependents)
            {
                await ProjectSingleAsync(dependent, processedEntities, cancellation);
            }
        }

        private readonly struct ScopedProjectionEngine
        {
            private readonly EntityDescriptor _entityDescriptor;
            private readonly IProjector _projector;
            private readonly ITransactionalDatabase _database;
            private readonly IServiceProvider _serviceProvider;

            private readonly IDictionary<EntityDescriptor, (EntityProjectionMetadata metadata, bool touched)> _metadataCache;

            public ScopedProjectionEngine(in EntityDescriptor entityDescriptor,
                              IProjector projector,
                              ITransactionalDatabase database,
                              IServiceProvider serviceProvider)
            {
                Assert(entityDescriptor != default);
                Assert(projector != null);
                Assert(database != null);
                Assert(serviceProvider != null);

                _entityDescriptor = entityDescriptor;
                _projector = projector;
                _database = database;
                _serviceProvider = serviceProvider;

                _metadataCache = new Dictionary<EntityDescriptor, (EntityProjectionMetadata metadata, bool touched)>();
            }

            // TODO: If all projections are deleted, we can remove the metadata fro the entity.
            public async ValueTask<IEnumerable<EntityDescriptor>> ProjectAsync(CancellationToken cancellation)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var scopedServiceProvider = scope.ServiceProvider;
                    var entityStorageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();
                    var entityStoragePropertyManager = scopedServiceProvider.GetRequiredService<IEntityStoragePropertyManager>(); // TODO: Rename
                    var entity = await entityStorageEngine.GetByIdAsync(_entityDescriptor.EntityType, _entityDescriptor.EntityId, cancellation);

                    if (await CheckIfUpdateNeededAsync(entity, cancellation))
                    {
                        await ProjectCoreAsync(entity, cancellation);
                        var dependencies = GetDependencies(entityStorageEngine);
                        await UpdateDependenciesAsync(dependencies, cancellation);
                    }
                }

                var dependents = await GetDependentsAsync(cancellation);

                foreach (var touchedMetadata in _metadataCache.Values.Where(p => p.touched).Select(p => p.metadata))
                {
                    await _database.StoreAsync(touchedMetadata, cancellation);
                }

                return dependents;
            }

            private async Task<bool> CheckIfUpdateNeededAsync(object entity, CancellationToken cancellation)
            {
                //var entityRevision = entityStoragePropertyManager.GetRevision(entity);
                //var projectionRevision = await metadataStorage.GetProjectionRevisionAsync(cancellation);
                //await metadataStorage.SetProjectionRevisionAsync(entityRevision, cancellation);
                return true; // TODO: Implement
            }

            private IEnumerable<EntityDescriptor> GetDependencies(IEntityStorageEngine entityStorageEngine)
            {
                var entityDescriptor = _entityDescriptor;

                bool IsCurrentEntity((Type type, string id, long revision, object entity) cacheEntry)
                {
                    return cacheEntry.id == entityDescriptor.EntityId && cacheEntry.type == entityDescriptor.EntityType;
                }

                return entityStorageEngine.CachedEntries
                                          .Where(p => !IsCurrentEntity(p) && p.revision == default)
                                          .Select(p => new EntityDescriptor(p.type, p.id))
                                          .ToArray();
            }

            private async ValueTask<IEnumerable<EntityDescriptor>> GetDependentsAsync(CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                return metadata.Dependents.Select(p => new EntityDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            private async Task UpdateDependenciesAsync(IEnumerable<EntityDescriptor> dependencies, CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                var storedDependencies = metadata.Dependencies.Select(p => new EntityDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
                var addedDependencies = dependencies.Except(storedDependencies);
                var removedDependencies = storedDependencies.Except(dependencies);

                metadata.Dependencies.Clear();
                metadata.Dependencies.AddRange(dependencies.Select(p => new Dependency { Id = p.EntityId, Type = p.EntityType.AssemblyQualifiedName }));

                _metadataCache[_entityDescriptor] = (metadata, touched: true);

                foreach (var added in addedDependencies)
                {
                    await AddDependentAsync(added, cancellation);
                }

                foreach (var removed in removedDependencies)
                {
                    await RemoveDependentAsync(removed, cancellation);
                }
            }

            private async Task AddDependentAsync(EntityDescriptor dependency,
                                                 CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var metadata = await GetMetadataAsync(dependency, cancellation);
                var dependent = new Dependent
                {
                    Id = _entityDescriptor.EntityId,
                    Type = _entityDescriptor.EntityType.AssemblyQualifiedName
                };

                metadata.Dependents.Add(dependent);
                _metadataCache[dependency] = (metadata, touched: true);
            }

            private async Task RemoveDependentAsync(EntityDescriptor dependency,
                                                    CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var entityDescriptor = _entityDescriptor;

                var metadata = await GetMetadataAsync(dependency, cancellation);
                var removed = metadata.Dependents.RemoveFirstWhere(p => p.Id.Equals(entityDescriptor.EntityId) &&
                                                                        p.Type == entityDescriptor.EntityType.AssemblyQualifiedName);

                Assert(removed != null);

                _metadataCache[dependency] = (metadata, touched: true);
            }

            #region ProjectCore

            private async Task ProjectCoreAsync(object entity, CancellationToken cancellation)
            {
                var entityType = _entityDescriptor.EntityType;
                var projectionResults = await _projector.ProjectAsync(entityType, entity, cancellation);

                foreach (var projectionResult in projectionResults)
                {
                    await _database.StoreAsync(projectionResult.ResultType, projectionResult.Result, cancellation);
                }

                var projections = projectionResults.Select(p => new ProjectionDescriptorX(p.ResultType, p.ResultId.ToString())); // TODO: Add a StringifiedResultId property.

                // Get currently applied projections and compare them with the new projections. Remove old projections.
                var appliedProjections = await GetAppliedProjectionsAsync(cancellation);
                var addedProjections = projections.Except(appliedProjections);
                var removedProjections = appliedProjections.Except(projections);

                foreach (var addedProjection in addedProjections)
                {
                    await AddEntityToProjectionAsync(addedProjection, cancellation);
                }

                foreach (var removedProjection in removedProjections)
                {
                    if (await RemoveEntityFromProjectionAsync(removedProjection, cancellation))
                    {
                        await _database.RemoveAsync(removedProjection.ProjectionType, null /* TODO */ , cancellation);
                    }
                }

                // Update the projections metadata in the database
                await UpdateAppliedProjectionsAsync(projections, cancellation);
            }

            private async Task<bool> RemoveEntityFromProjectionAsync(ProjectionDescriptorX removedProjection, CancellationToken cancellation)
            {
                // TODO: Implement

                return true;
            }

            private async Task AddEntityToProjectionAsync(ProjectionDescriptorX addedProjection, CancellationToken cancellation)
            {
                // TODO: Implement
            }

            private async ValueTask<IEnumerable<ProjectionDescriptorX>> GetAppliedProjectionsAsync(CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                return metadata.Projections.Select(p => new ProjectionDescriptorX(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            private async Task UpdateAppliedProjectionsAsync(IEnumerable<ProjectionDescriptorX> projections, CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                metadata.Projections.Clear();
                metadata.Projections.AddRange(projections.Select(p => new Projection { Type = p.ProjectionType.AssemblyQualifiedName, Id = p.ProjectionId }));
                _metadataCache[_entityDescriptor] = (metadata, touched: true);
            }

            #endregion

            #region EntityMetadata

            private ValueTask<EntityProjectionMetadata> GetMetadataAsync(CancellationToken cancellation)
            {
                return GetMetadataAsync(_entityDescriptor, cancellation);
            }

            private async ValueTask<EntityProjectionMetadata> GetMetadataAsync(EntityDescriptor entityDescriptor,
                                                                               CancellationToken cancellation)
            {
                if (!_metadataCache.TryGetValue(entityDescriptor, out var entry))
                {
                    var entryId = IdGenerator.GenerateId(entityDescriptor.EntityType.AssemblyQualifiedName, entityDescriptor.EntityId);
                    var metadata = (await _database.GetAsync<EntityProjectionMetadata>(p => p.Id == entryId, cancellation)).FirstOrDefault();
                    var touched = false;

                    if (metadata == null)
                    {
                        metadata = new EntityProjectionMetadata
                        {
                            EntityId = entityDescriptor.EntityId,
                            EntityType = entityDescriptor.EntityType.AssemblyQualifiedName
                        };

                        touched = true;
                    }

                    entry = (metadata, touched);
                    _metadataCache.Add(entityDescriptor, entry);
                }

                return entry.metadata;
            }

            private sealed class EntityProjectionMetadata
            {
                private string _id;

                public string Id
                {
                    get
                    {
                        if (_id == null)
                        {
                            _id = IdGenerator.GenerateId(EntityId, EntityType);
                        }

                        return _id;
                    }
                    set => _id = value;
                }

                public string EntityId { get; set; }
                public string EntityType { get; set; }
                public long ProjectionRevision { get; set; }
                public List<Projection> Projections { get; } = new List<Projection>();
                public List<Dependency> Dependencies { get; } = new List<Dependency>();
                public List<Dependent> Dependents { get; } = new List<Dependent>();
            }

            private sealed class Dependency
            {
                public string Id { get; set; }
                public string Type { get; set; }

                public long ProjectionRevision { get; }
            }

            private sealed class Dependent
            {
                public string Id { get; set; }
                public string Type { get; set; }
            }

            private sealed class Projection
            {
                public string Type { get; set; }
                public string Id { get; set; }
            }

            #endregion
        }
    }
}
