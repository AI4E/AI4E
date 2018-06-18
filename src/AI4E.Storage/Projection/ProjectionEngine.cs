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

            var processedEntities = new HashSet<ProjectionSourceDescriptor>();
            return ProjectSingleAsync(new ProjectionSourceDescriptor(entityType, id), processedEntities, cancellation);
        }

        private async Task ProjectSingleAsync(ProjectionSourceDescriptor entityDescriptor,
                                              ISet<ProjectionSourceDescriptor> processedEntities,
                                              CancellationToken cancellation)
        {
            if (processedEntities.Contains(entityDescriptor))
            {
                return;
            }

            IEnumerable<ProjectionSourceDescriptor> dependents;

            ITransactionalDatabase database = null;
            try
            {
                do
                {
                    if (database != null)
                    {
                        await database.DisposeIfDisposableAsync();
                    }

                    database = _databaseFactory.ProvideInstance();
                    var scopedEngine = new ScopedProjectionEngine(entityDescriptor, _projector, database, _serviceProvider);
                    dependents = await scopedEngine.ProjectAsync(cancellation);
                }
                while (!await database.TryCommitAsync(cancellation));
            }
            finally
            {
                if (database != null)
                {
                    await database.DisposeIfDisposableAsync();
                }
            }

            processedEntities.Add(entityDescriptor);

            foreach (var dependent in dependents)
            {
                await ProjectSingleAsync(dependent, processedEntities, cancellation);
            }
        }

        private readonly struct ScopedProjectionEngine
        {
            private readonly ProjectionSourceDescriptor _sourceDescriptor;
            private readonly IProjector _projector;
            private readonly ITransactionalDatabase _database;
            private readonly IServiceProvider _serviceProvider;

            private readonly IDictionary<ProjectionSourceDescriptor, ProjectionSourceMetadataCacheEntry> _sourceMetadataCache;
            private readonly IDictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry> _targetMetadataCache;

            private readonly struct ProjectionSourceMetadataCacheEntry
            {
                public ProjectionSourceMetadataCacheEntry(ProjectionSourceMetadata metadata, bool touched)
                {
                    Metadata = metadata;
                    Touched = touched;
                }

                public ProjectionSourceMetadata Metadata { get; }
                public bool Touched { get; }
            }

            private readonly struct ProjectionTargetMetadataCacheEntry
            {
                public ProjectionTargetMetadataCacheEntry(ProjectionTargetMetadata metadata, bool touched)
                {
                    Metadata = metadata;
                    Touched = touched;
                }

                public ProjectionTargetMetadata Metadata { get; }
                public bool Touched { get; }
            }

            public ScopedProjectionEngine(in ProjectionSourceDescriptor sourceDescriptor,
                              IProjector projector,
                              ITransactionalDatabase database,
                              IServiceProvider serviceProvider)
            {
                Assert(sourceDescriptor != default);
                Assert(projector != null);
                Assert(database != null);
                Assert(serviceProvider != null);

                _sourceDescriptor = sourceDescriptor;
                _projector = projector;
                _database = database;
                _serviceProvider = serviceProvider;

                _sourceMetadataCache = new Dictionary<ProjectionSourceDescriptor, ProjectionSourceMetadataCacheEntry>();
                _targetMetadataCache = new Dictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry>();
            }

            // TODO: If all projections are deleted, we can remove the metadata from the entity.
            public async ValueTask<IEnumerable<ProjectionSourceDescriptor>> ProjectAsync(CancellationToken cancellation)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var scopedServiceProvider = scope.ServiceProvider;
                    var entityStorageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();
                    var entityStoragePropertyManager = scopedServiceProvider.GetRequiredService<IEntityStoragePropertyManager>(); // TODO: Rename
                    var entity = await entityStorageEngine.GetByIdAsync(_sourceDescriptor.SourceType, _sourceDescriptor.SourceId, cancellation);

                    if (await CheckIfUpdateNeededAsync(entity, cancellation))
                    {
                        await ProjectCoreAsync(entity, scopedServiceProvider, cancellation);
                        var dependencies = GetDependencies(entityStorageEngine);
                        await UpdateDependenciesAsync(dependencies, cancellation);
                    }
                }

                var dependents = await GetDependentsAsync(cancellation);

                // Write touched source metadata to database
                foreach (var touchedMetadata in _sourceMetadataCache.Values.Where(p => p.Touched).Select(p => p.Metadata))
                {
                    if (touchedMetadata == null)
                    {
                        await _database.RemoveAsync(touchedMetadata, cancellation);
                    }
                    else
                    {
                        await _database.StoreAsync(touchedMetadata, cancellation);
                    }
                }

                // Write touched target metadata to database
                foreach (var touchedMetadata in _targetMetadataCache.Values.Where(p => p.Touched).Select(p => p.Metadata))
                {
                    if (touchedMetadata == null)
                    {
                        await _database.RemoveAsync(touchedMetadata, cancellation);
                    }
                    else
                    {
                        await _database.StoreAsync(touchedMetadata, cancellation);
                    }
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

            private IEnumerable<ProjectionSourceDescriptor> GetDependencies(IEntityStorageEngine entityStorageEngine)
            {
                var entityDescriptor = _sourceDescriptor;

                bool IsCurrentEntity((Type type, string id, long revision, object entity) cacheEntry)
                {
                    return cacheEntry.id == entityDescriptor.SourceId && cacheEntry.type == entityDescriptor.SourceType;
                }

                return entityStorageEngine.CachedEntries
                                          .Where(p => !IsCurrentEntity(p) && p.revision == default)
                                          .Select(p => new ProjectionSourceDescriptor(p.type, p.id))
                                          .ToArray();
            }

            private async ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                return metadata.Dependents.Select(p => new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            private async Task UpdateDependenciesAsync(IEnumerable<ProjectionSourceDescriptor> dependencies, CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                var storedDependencies = metadata.Dependencies.Select(p => new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));

                foreach (var added in dependencies.Except(storedDependencies))
                {
                    await AddDependentAsync(added, cancellation);
                }

                foreach (var removed in storedDependencies.Except(dependencies))
                {
                    await RemoveDependentAsync(removed, cancellation);
                }

                metadata.Dependencies.Clear();
                metadata.Dependencies.AddRange(dependencies.Select(p => new Dependency { Id = p.SourceId, Type = StringifyType(p.SourceType) }));

                _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(metadata, touched: true);    
            }

            private async Task AddDependentAsync(ProjectionSourceDescriptor dependency,
                                                 CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var metadata = await GetMetadataAsync(dependency, cancellation);
                var dependent = new Dependent
                {
                    Id = _sourceDescriptor.SourceId,
                    Type = StringifyType(_sourceDescriptor.SourceType)
                };

                metadata.Dependents.Add(dependent);
                _sourceMetadataCache[dependency] = new ProjectionSourceMetadataCacheEntry(metadata, touched: true);
            }

            private async Task RemoveDependentAsync(ProjectionSourceDescriptor dependency,
                                                    CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var entityDescriptor = _sourceDescriptor;

                var metadata = await GetMetadataAsync(dependency, cancellation);
                var removed = metadata.Dependents.RemoveFirstWhere(p => p.Id.Equals(entityDescriptor.SourceId) &&
                                                                        p.Type == StringifyType(entityDescriptor.SourceType));

                Assert(removed != null);

                _sourceMetadataCache[dependency] = new ProjectionSourceMetadataCacheEntry(metadata, touched: true);
            }

            #region ProjectCore

            private async Task ProjectCoreAsync(object entity, IServiceProvider serviceProvider, CancellationToken cancellation)
            {
                var entityType = _sourceDescriptor.SourceType;
                var projectionResults = await _projector.ProjectAsync(entityType, entity, serviceProvider, cancellation);

                foreach (var projectionResult in projectionResults)
                {
                    await _database.StoreAsync(projectionResult.ResultType, projectionResult.Result, cancellation);
                }

                var projections = projectionResults.Select(p => new ProjectionTargetDescriptor(p.ResultType, p.ResultId.ToString())); // TODO: Add a StringifiedResultId property.

                // Get currently applied projections and compare them to the new projections. Remove old projections.
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
                        await _database.RemoveAsync(removedProjection.TargetType, null /* TODO */ , cancellation);
                    }
                }

                // Update the projections metadata in the database
                await UpdateAppliedProjectionsAsync(projections, cancellation);
            }

            private async Task<bool> RemoveEntityFromProjectionAsync(ProjectionTargetDescriptor removedProjection, CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(removedProjection, cancellation);
                var sourceDescriptor = _sourceDescriptor;
                var removed = metadata.ProjectionSources.RemoveFirstWhere(p => p.Id == sourceDescriptor.SourceId &&
                                                                               p.Type == StringifyType(sourceDescriptor.SourceType));

                Assert(removed != null);

                if (!metadata.ProjectionSources.Any())
                {
                    _targetMetadataCache[removedProjection] = new ProjectionTargetMetadataCacheEntry(metadata: null, touched: true);

                    return true;
                }

                _targetMetadataCache[removedProjection] = new ProjectionTargetMetadataCacheEntry(metadata, touched: true);

                return false;
            }

            private async Task AddEntityToProjectionAsync(ProjectionTargetDescriptor addedProjection, CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(addedProjection, cancellation);

                var source = _sourceDescriptor;

                Assert(!metadata.ProjectionSources.Any(p => p.Id == source.SourceId && p.Type == StringifyType(source.SourceType)));

                metadata.ProjectionSources.Add(new ProjectionSource { Id = source.SourceId, Type = StringifyType(source.SourceType) });

                _targetMetadataCache[addedProjection] = new ProjectionTargetMetadataCacheEntry(metadata, touched: true);
            }

            private async ValueTask<IEnumerable<ProjectionTargetDescriptor>> GetAppliedProjectionsAsync(CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                return metadata.ProjectionTargets.Select(p => new ProjectionTargetDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            private async Task UpdateAppliedProjectionsAsync(IEnumerable<ProjectionTargetDescriptor> projections, CancellationToken cancellation)
            {
                var metadata = await GetMetadataAsync(cancellation);
                metadata.ProjectionTargets.Clear();
                metadata.ProjectionTargets.AddRange(projections.Select(p => new ProjectionTarget { Type = StringifyType(p.TargetType), Id = p.TargetId }));
                _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(metadata, touched: true);
            }

            #endregion

            #region ProjectionSourceMetadata

            private ValueTask<ProjectionSourceMetadata> GetMetadataAsync(CancellationToken cancellation)
            {
                return GetMetadataAsync(_sourceDescriptor, cancellation);
            }

            private async ValueTask<ProjectionSourceMetadata> GetMetadataAsync(ProjectionSourceDescriptor sourceDescriptor,
                                                                               CancellationToken cancellation)
            {
                if (!_sourceMetadataCache.TryGetValue(sourceDescriptor, out var entry))
                {
                    var entryId = ProjectionSourceMetadata.GenerateId(sourceDescriptor.SourceId, StringifyType(sourceDescriptor.SourceType));
                    var metadata = (await _database.GetAsync<ProjectionSourceMetadata>(p => p.Id == entryId, cancellation)).FirstOrDefault();
                    var touched = false;

                    if (metadata == null)
                    {
                        metadata = new ProjectionSourceMetadata
                        {
                            SourceId = sourceDescriptor.SourceId,
                            SourceType = StringifyType(sourceDescriptor.SourceType)
                        };

                        touched = true;
                    }

                    entry = new ProjectionSourceMetadataCacheEntry(metadata, touched);
                    _sourceMetadataCache.Add(sourceDescriptor, entry);
                }

                return entry.Metadata;
            }

            private sealed class ProjectionSourceMetadata
            {
                private string _id;

                public static string GenerateId(string sourceId, string sourceType)
                {
                    return IdGenerator.GenerateId(sourceId, sourceType);
                }

                public string Id
                {
                    get
                    {
                        if (_id == null)
                        {
                            _id = GenerateId(SourceId, SourceType);
                        }

                        return _id;
                    }
                    set => _id = value;
                }

                public string SourceId { get; set; }
                public string SourceType { get; set; }
                public long ProjectionRevision { get; set; }
                public List<ProjectionTarget> ProjectionTargets { get; } = new List<ProjectionTarget>();
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

            private sealed class ProjectionTarget
            {
                public string Id { get; set; }
                public string Type { get; set; }
            }

            #endregion

            #region ProjectionTargetMetadata

            private async ValueTask<ProjectionTargetMetadata> GetMetadataAsync(ProjectionTargetDescriptor target,
                                                                               CancellationToken cancellation)
            {
                if (!_targetMetadataCache.TryGetValue(target, out var entry))
                {
                    var entryId = ProjectionTargetMetadata.GenerateId(target.TargetId, StringifyType(target.TargetType));
                    var metadata = (await _database.GetAsync<ProjectionTargetMetadata>(p => p.Id == entryId, cancellation)).FirstOrDefault();
                    var touched = false;

                    if (metadata == null)
                    {
                        metadata = new ProjectionTargetMetadata
                        {
                            TargetId = target.TargetId,
                            TargetType = StringifyType(target.TargetType)
                        };

                        touched = true;
                    }

                    entry = new ProjectionTargetMetadataCacheEntry(metadata, touched);
                    _targetMetadataCache.Add(target, entry);
                }

                return entry.Metadata;
            }

            private sealed class ProjectionTargetMetadata
            {
                private string _id;

                public static string GenerateId(string targetId, string targetType)
                {
                    return IdGenerator.GenerateId(targetId, targetType);
                }

                public string Id
                {
                    get
                    {
                        if (_id == null)
                        {
                            _id = GenerateId(TargetId, TargetType);
                        }

                        return _id;
                    }
                    set => _id = value;
                }

                public string TargetId { get; set; }
                public string TargetType { get; set; }
                public List<ProjectionSource> ProjectionSources { get; } = new List<ProjectionSource>();
            }

            private sealed class ProjectionSource
            {
                public string Id { get; set; }
                public string Type { get; set; }
            }

            #endregion

            private static string StringifyType(Type t)
            {
                return t.ToString();
            }
        }
    }
}
