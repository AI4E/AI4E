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
        private readonly ITransactionalDatabase _database;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProjectionEngine> _logger;

        public ProjectionEngine(IProjector projector,
                                ITransactionalDatabase database,
                                IServiceProvider serviceProvider,
                                ILogger<ProjectionEngine> logger = default)
        {
            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (database == null)
                throw new ArgumentNullException(nameof(database));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _projector = projector;
            _database = database;
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
            return ProjectAsync(new ProjectionSourceDescriptor(entityType, id), processedEntities, cancellation);
        }

        private async Task ProjectAsync(ProjectionSourceDescriptor entityDescriptor,
                                        ISet<ProjectionSourceDescriptor> processedEntities,
                                        CancellationToken cancellation)
        {
            if (processedEntities.Contains(entityDescriptor))
            {
                return;
            }

            IEnumerable<ProjectionSourceDescriptor> dependents;

            IScopedTransactionalDatabase database = null;
            try
            {
                do
                {
                    database?.Dispose();

                    database = _database.CreateScope();
                    var scopedEngine = new SourceScopedProjectionEngine(entityDescriptor, _projector, database, _serviceProvider);
                    dependents = await scopedEngine.ProjectAsync(cancellation);
                }
                while (!await database.TryCommitAsync(cancellation));
            }
            finally
            {
                database?.Dispose();
            }

            processedEntities.Add(entityDescriptor);

            foreach (var dependent in dependents)
            {
                await ProjectAsync(dependent, processedEntities, cancellation);
            }
        }

        private static string StringifyType(Type t)
        {
            return t.ToString();
        }

        #region Scoped engines

        private readonly struct SourceScopedProjectionEngine
        {
            private readonly ProjectionSourceDescriptor _sourceDescriptor;
            private readonly IProjector _projector;
            private readonly IScopedTransactionalDatabase _database;
            private readonly IServiceProvider _serviceProvider;

            private readonly IDictionary<ProjectionSourceDescriptor, ProjectionSourceMetadataCacheEntry> _sourceMetadataCache;
            private readonly IDictionary<Type, ITargetScopedProjectionEngine> _targetScopedProjectionEngines;

            public SourceScopedProjectionEngine(in ProjectionSourceDescriptor sourceDescriptor,
                                                IProjector projector,
                                                IScopedTransactionalDatabase database,
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
                _targetScopedProjectionEngines = new Dictionary<Type, ITargetScopedProjectionEngine>();
            }

            public async ValueTask<IEnumerable<ProjectionSourceDescriptor>> ProjectAsync(CancellationToken cancellation)
            {
                do
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var scopedServiceProvider = scope.ServiceProvider;
                        //var storageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();
                        //var propertyManager = scopedServiceProvider.GetRequiredService<IEntityStoragePropertyManager>(); // TODO: Rename
                        var projectionSourceLoader = scopedServiceProvider.GetRequiredService<IProjectionSourceLoader>();
                        var (source, sourceRevision) = await projectionSourceLoader.LoadAsync(_sourceDescriptor.SourceType, _sourceDescriptor.SourceId, cancellation);

                        var (updateNeeded, conflict) = await GetProjectionStateAsync(projectionSourceLoader, source, sourceRevision, cancellation);

                        if (conflict)
                        {
                            // TODO: Log conflict
                            // TODO: Currently we are allocating a completely new storage engine. It is more performant to reuse the current storageEngine. 
                            //       For this to work, it needs a way to flush its cache and refresh all cached streams on entity load.
                            continue;
                        }

                        if (updateNeeded &&
                            await ProjectCoreAsync(source, scopedServiceProvider, cancellation))
                        {
                            // Only update dependencies if there are any projections.
                            var dependencies = GetDependencies(projectionSourceLoader);
                            await UpdateDependenciesAsync(dependencies, cancellation);
                        }

                        break;
                    }
                }
                while (true);

                var dependents = await GetDependentsAsync(cancellation);

                // Write touched source metadata to database
                foreach (var (originalMetadata, touchedMetadata) in _sourceMetadataCache.Values.Where(p => p.Touched))
                {
                    if (touchedMetadata == null)
                    {
                        Assert(originalMetadata != null);

                        await _database.RemoveAsync(originalMetadata, cancellation);
                    }
                    else
                    {
                        await _database.StoreAsync(touchedMetadata, cancellation);
                    }
                }

                foreach (var targetTypedScopedEngine in _targetScopedProjectionEngines.Values)
                {
                    await targetTypedScopedEngine.WriteToDatabaseAsync(cancellation);
                }

                return dependents;
            }

            private async ValueTask<(bool updateNeeded, bool conflict)> GetProjectionStateAsync(IProjectionSourceLoader projectionSourceLoader,
                                                                                                object source,
                                                                                                long sourceRevision,
                                                                                                CancellationToken cancellation)
            {
                // We load all dependencies from the entity store. 
                // As a projection source's dependencies do not change often, chances are good that the 
                // entities are cached when accessed within the projection phase.
                // For that reason, performance should not suffer very much 
                // in comparison to not checking whether an update is needed.

                var (originalMetadata, metadata) = await GetMetadataAsync(createIfNonExistent: false, cancellation);

                if (metadata == null)
                {
                    return (updateNeeded: true, conflict: false);
                }

                var projectionRevision = metadata.ProjectionRevision;
                var projectionLaterEntity = sourceRevision < projectionRevision;
                var entityLaterProjection = sourceRevision > projectionRevision;

                (ProjectionSourceDescriptor dependency, long revision) ToDependency(Dependency p)
                {
                    return (new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id), p.ProjectionRevision);
                }

                foreach (var (dependency, dependencyProjectionRevision) in metadata.Dependencies.Select(p => ToDependency(p)))
                {
                    var (dependencyEntity, dependencyRevision) = await projectionSourceLoader.LoadAsync(dependency.SourceType, dependency.SourceId, cancellation);

                    if (dependencyRevision < dependencyProjectionRevision)
                    {
                        projectionLaterEntity = true;
                    }
                    else if (dependencyRevision > dependencyProjectionRevision)
                    {
                        entityLaterProjection = true;
                    }
                }

                var updateNeeded = entityLaterProjection && !projectionLaterEntity;
                var conflict = entityLaterProjection && projectionLaterEntity;

                if (updateNeeded)
                {
                    metadata.ProjectionRevision = sourceRevision;
                    _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
                }


                return (updateNeeded, conflict);
            }

            private IEnumerable<(ProjectionSourceDescriptor dependency, long revision)> GetDependencies(IProjectionSourceLoader projectionSourceLoader)
            {
                var entityDescriptor = _sourceDescriptor;

                bool IsCurrentEntity((Type type, string id, long revision) cacheEntry)
                {
                    return cacheEntry.id == entityDescriptor.SourceId && cacheEntry.type == entityDescriptor.SourceType;
                }

                return projectionSourceLoader.LoadedSources
                                             .Where(p => !IsCurrentEntity(p))
                                             .Select(p => (new ProjectionSourceDescriptor(p.type, p.id), p.revision))
                                             .ToArray();
            }

            private async ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation)
            {
                var (_, metadata) = await GetMetadataAsync(createIfNonExistent: false, cancellation);

                if (metadata == null)
                {
                    return Enumerable.Empty<ProjectionSourceDescriptor>();
                }

                return metadata.Dependents.Select(p => new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            private async Task UpdateDependenciesAsync(IEnumerable<(ProjectionSourceDescriptor dependency, long revision)> dependencies, CancellationToken cancellation)
            {
                var (originalMetadata, metadata) = await GetMetadataAsync(createIfNonExistent: true, cancellation);
                var storedDependencies = metadata.Dependencies.Select(p => new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));

                foreach (var added in dependencies.Select(p => p.dependency).Except(storedDependencies))
                {
                    await AddDependentAsync(added, cancellation);
                }

                foreach (var removed in storedDependencies.Except(dependencies.Select(p => p.dependency)))
                {
                    await RemoveDependentAsync(removed, cancellation);
                }

                Dependency ToStoredDependency((ProjectionSourceDescriptor dependency, long revision) p)
                {
                    return new Dependency
                    {
                        Id = p.dependency.SourceId,
                        Type = StringifyType(p.dependency.SourceType),
                        ProjectionRevision = p.revision
                    };
                }

                metadata.Dependencies.Clear();
                metadata.Dependencies.AddRange(dependencies.Select(p => ToStoredDependency(p)));

                _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
            }

            private async Task AddDependentAsync(ProjectionSourceDescriptor dependency,
                                                 CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var (originalMetadata, metadata) = await GetMetadataAsync(dependency, createIfNonExistent: true, cancellation);
                var dependent = new Dependent
                {
                    Id = _sourceDescriptor.SourceId,
                    Type = StringifyType(_sourceDescriptor.SourceType)
                };

                metadata.Dependents.Add(dependent);
                _sourceMetadataCache[dependency] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
            }

            private async Task RemoveDependentAsync(ProjectionSourceDescriptor dependency,
                                                    CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var entityDescriptor = _sourceDescriptor;

                var (originalMetadata, metadata) = await GetMetadataAsync(dependency, createIfNonExistent: true, cancellation);
                var removed = metadata.Dependents.RemoveFirstWhere(p => p.Id.Equals(entityDescriptor.SourceId) &&
                                                                        p.Type == StringifyType(entityDescriptor.SourceType));

                Assert(removed != null);

                if (!metadata.ProjectionTargets.Any() && !metadata.Dependents.Any())
                {
                    Assert(!metadata.Dependencies.Any());

                    _sourceMetadataCache[dependency] = new ProjectionSourceMetadataCacheEntry(originalMetadata,
                                                                                              metadata: null,
                                                                                              touched: true);
                    return;
                }

                _sourceMetadataCache[dependency] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
            }

            #region ProjectCore

            private async Task<bool> ProjectCoreAsync(object entity, IServiceProvider serviceProvider, CancellationToken cancellation)
            {
                var entityType = _sourceDescriptor.SourceType;
                var projectionResults = await _projector.ProjectAsync(entityType, entity, serviceProvider, cancellation);
                var appliedProjections = new HashSet<ProjectionTargetDescriptor>(await GetAppliedProjectionsAsync(cancellation));
                var projections = new List<ProjectionTargetDescriptor>();

                // TODO: Ensure that there are no two projection results with the same type and id. 
                //       Otherwise bad things happen.
                foreach (var projectionResult in projectionResults)
                {
                    var projection = new ProjectionTargetDescriptor(projectionResult.ResultType,
                                                                    projectionResult.ResultId.ToString());
                    projections.Add(projection);

                    if (!appliedProjections.Remove(projection))
                    {
                        await AddEntityToProjectionAsync(projectionResult, cancellation);
                    }

                    await _database.StoreAsync(projectionResult.ResultType, projectionResult.Result, cancellation);
                }

                // We removed all current projections from applied projections. 
                // The remaining ones are removed projections.
                foreach (var removedProjection in appliedProjections)
                {
                    await RemoveEntityFromProjectionAsync(removedProjection, cancellation);
                }

                // Update the projections metadata in the database
                await UpdateAppliedProjectionsAsync(projections, cancellation);

                return projectionResults.Any();
            }

            private ITargetScopedProjectionEngine GetTargetScopedProjectionEngine(Type targetType)
            {
                if (!_targetScopedProjectionEngines.TryGetValue(targetType, out var result))
                {
                    var typeDefinition = typeof(TargetScopedProjectionEngine<,>);
                    var idType = DataPropertyHelper.GetIdType(targetType);
                    var type = typeDefinition.MakeGenericType(idType, targetType);

                    result = (ITargetScopedProjectionEngine)Activator.CreateInstance(type, _database);
                    _targetScopedProjectionEngines.Add(targetType, result);
                }

                return result;
            }

            private Task RemoveEntityFromProjectionAsync(ProjectionTargetDescriptor removedProjection,
                                                               CancellationToken cancellation)
            {
                var targetScopedProjectionEngine = GetTargetScopedProjectionEngine(removedProjection.TargetType);

                return targetScopedProjectionEngine.RemoveEntityFromProjectionAsync(_sourceDescriptor, removedProjection, cancellation);
            }

            private Task AddEntityToProjectionAsync(IProjectionResult projectionResult,
                                                          CancellationToken cancellation)
            {
                var targetScopedProjectionEngine = GetTargetScopedProjectionEngine(projectionResult.ResultType);

                return targetScopedProjectionEngine.AddEntityToProjectionAsync(_sourceDescriptor, projectionResult, cancellation);
            }

            private async ValueTask<IEnumerable<ProjectionTargetDescriptor>> GetAppliedProjectionsAsync(CancellationToken cancellation)
            {
                var (_, metadata) = await GetMetadataAsync(createIfNonExistent: false, cancellation);

                if (metadata == null)
                    return Enumerable.Empty<ProjectionTargetDescriptor>();

                return metadata.ProjectionTargets.Select(p => new ProjectionTargetDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            private async Task UpdateAppliedProjectionsAsync(IEnumerable<ProjectionTargetDescriptor> projections, CancellationToken cancellation)
            {
                var (originalMetadata, metadata) = await GetMetadataAsync(createIfNonExistent: true, cancellation);

                if (!projections.Any())
                {
                    if (!metadata.Dependents.Any())
                    {
                        _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(originalMetadata,
                                                                                                         metadata: null,
                                                                                                         touched: true);
                        return;
                    }

                    await UpdateDependenciesAsync(Enumerable.Empty<(ProjectionSourceDescriptor dependency, long revision)>(), cancellation);
                }

                metadata.ProjectionTargets.Clear();
                metadata.ProjectionTargets.AddRange(projections.Select(p => new ProjectionTarget { Type = StringifyType(p.TargetType), Id = p.TargetId }));
                _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
            }

            #endregion

            private ValueTask<ProjectionSourceMetadataCacheEntry> GetMetadataAsync(bool createIfNonExistent, CancellationToken cancellation)
            {
                return GetMetadataAsync(_sourceDescriptor, createIfNonExistent, cancellation);
            }

            private async ValueTask<ProjectionSourceMetadataCacheEntry> GetMetadataAsync(ProjectionSourceDescriptor sourceDescriptor,
                                                                                         bool createIfNonExistent,
                                                                                         CancellationToken cancellation)
            {
                if (!_sourceMetadataCache.TryGetValue(sourceDescriptor, out var entry))
                {
                    var entryId = ProjectionSourceMetadata.GenerateId(sourceDescriptor.SourceId, StringifyType(sourceDescriptor.SourceType));
                    var metadata = await _database.GetAsync<ProjectionSourceMetadata>(p => p.Id == entryId, cancellation).FirstOrDefault();
                    var originalMetadata = metadata;
                    var touched = false;

                    if (metadata == null && createIfNonExistent)
                    {
                        metadata = new ProjectionSourceMetadata
                        {
                            SourceId = sourceDescriptor.SourceId,
                            SourceType = StringifyType(sourceDescriptor.SourceType)
                        };

                        touched = true;
                    }

                    entry = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched);
                    _sourceMetadataCache.Add(sourceDescriptor, entry);
                }
                else if (entry.Metadata == null && createIfNonExistent)
                {
                    var metadata = new ProjectionSourceMetadata
                    {
                        SourceId = sourceDescriptor.SourceId,
                        SourceType = StringifyType(sourceDescriptor.SourceType)
                    };

                    entry = new ProjectionSourceMetadataCacheEntry(entry.OriginalMetadata, metadata, touched: true);
                    _sourceMetadataCache[sourceDescriptor] = entry;
                }

                return entry;
            }

            private readonly struct ProjectionSourceMetadataCacheEntry
            {
                public ProjectionSourceMetadataCacheEntry(ProjectionSourceMetadata originalMetadata, ProjectionSourceMetadata metadata, bool touched)
                {
                    if (originalMetadata == null && metadata == null)
                        touched = false;

                    OriginalMetadata = originalMetadata;
                    Metadata = metadata;
                    Touched = touched;
                }

                public ProjectionSourceMetadata OriginalMetadata { get; }
                public ProjectionSourceMetadata Metadata { get; }
                public bool Touched { get; }

                public void Deconstruct(out ProjectionSourceMetadata originalMetadata,
                                        out ProjectionSourceMetadata metadata)
                {
                    originalMetadata = OriginalMetadata;
                    metadata = Metadata;
                }
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

                public long ProjectionRevision { get; set; }
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
        }

        private interface ITargetScopedProjectionEngine
        {
            Task RemoveEntityFromProjectionAsync(ProjectionSourceDescriptor projectionSource, ProjectionTargetDescriptor removedProjection, CancellationToken cancellation);

            Task AddEntityToProjectionAsync(ProjectionSourceDescriptor projectionSource, IProjectionResult projectionResult, CancellationToken cancellation);

            Task WriteToDatabaseAsync(CancellationToken cancellation);
        }

        private sealed class TargetScopedProjectionEngine<TProjectionId, TProjection> : ITargetScopedProjectionEngine
            where TProjection : class
        {
            private readonly IDictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry> _targetMetadataCache;
            private readonly IScopedTransactionalDatabase _database;

            public TargetScopedProjectionEngine(IScopedTransactionalDatabase database)
            {
                _targetMetadataCache = new Dictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry>();
                _database = database;
            }

            public async Task WriteToDatabaseAsync(CancellationToken cancellation)
            {
                // Write touched target metadata to database
                foreach (var (originalMetadata, touchedMetadata) in _targetMetadataCache.Values.Where(p => p.Touched))
                {
                    if (touchedMetadata == null)
                    {
                        Assert(originalMetadata != null);

                        await _database.RemoveAsync(originalMetadata, cancellation);
                    }
                    else
                    {
                        await _database.StoreAsync(touchedMetadata, cancellation);
                    }
                }
            }

            public async Task AddEntityToProjectionAsync(ProjectionSourceDescriptor projectionSource,
                                                         IProjectionResult projectionResult,
                                                         CancellationToken cancellation)
            {
                var projectionId = GetProjectionId(projectionResult);

                var addedProjection = new ProjectionTargetDescriptor<TProjectionId>(typeof(TProjection), projectionId);
                var (originalMetadata, metadata) = await GetMetadataAsync(addedProjection, cancellation);

                Assert(!metadata.ProjectionSources.Any(p => p.Id == projectionSource.SourceId &&
                                                            p.Type == StringifyType(projectionSource.SourceType)));

                var storedProjectionSource = new ProjectionSource
                {
                    Id = projectionSource.SourceId,
                    Type = StringifyType(projectionSource.SourceType)
                };

                metadata.ProjectionSources.Add(storedProjectionSource);

                _targetMetadataCache[addedProjection] = new ProjectionTargetMetadataCacheEntry(originalMetadata,
                                                                                               metadata,
                                                                                               touched: true);
            }



            public async Task RemoveEntityFromProjectionAsync(ProjectionSourceDescriptor projectionSource,
                                                              ProjectionTargetDescriptor removedProjection,
                                                              CancellationToken cancellation)
            {
                var (originalMetadata, metadata) = await GetMetadataAsync(removedProjection, cancellation);

                if (metadata == null)
                {
                    Assert(false);
                    return;
                }

                var removed = metadata.ProjectionSources
                                      .RemoveFirstWhere(p => p.Id == projectionSource.SourceId &&
                                                             p.Type == StringifyType(projectionSource.SourceType));

                Assert(removed != null);

                if (!metadata.ProjectionSources.Any())
                {
                    _targetMetadataCache[removedProjection] = new ProjectionTargetMetadataCacheEntry(originalMetadata,
                                                                                                     metadata: null,
                                                                                                     touched: true);

                    var predicate = DataPropertyHelper.BuildPredicate<TProjectionId, TProjection>(metadata.TargetId);
                    var projection = await _database.GetAsync(predicate, cancellation).FirstOrDefault();

                    if (projection != null)
                    {
                        await _database.RemoveAsync(projection, cancellation);
                    }
                }

                _targetMetadataCache[removedProjection] = new ProjectionTargetMetadataCacheEntry(originalMetadata,
                                                                                                 metadata,
                                                                                                 touched: true);
            }

            private static TProjectionId GetProjectionId(IProjectionResult projectionResult)
            {
                TProjectionId projectionId;

                if (projectionResult is IProjectionResult<TProjectionId, TProjection> typedProjectionResult)
                {
                    Assert(typedProjectionResult.ResultType == typeof(TProjection));

                    projectionId = typedProjectionResult.ResultId;
                }
                else
                {
                    Assert(projectionResult != null);
                    Assert(projectionResult.ResultType == typeof(TProjection));
                    Assert(projectionResult.ResultId != null);
                    Assert(projectionResult.ResultId is TProjectionId);

                    projectionId = (TProjectionId)projectionResult.ResultId;
                }

                return projectionId;
            }

            private readonly struct ProjectionTargetMetadataCacheEntry
            {
                public ProjectionTargetMetadataCacheEntry(ProjectionTargetMetadata originalMetadata,
                                                          ProjectionTargetMetadata metadata,
                                                          bool touched)
                {
                    if (originalMetadata == null && metadata == null)
                        touched = false;

                    OriginalMetadata = originalMetadata;
                    Metadata = metadata;
                    Touched = touched;
                }

                public ProjectionTargetMetadata OriginalMetadata { get; }
                public ProjectionTargetMetadata Metadata { get; }
                public bool Touched { get; }

                public void Deconstruct(out ProjectionTargetMetadata originalMetadata,
                                        out ProjectionTargetMetadata metadata)
                {
                    originalMetadata = OriginalMetadata;
                    metadata = Metadata;
                }
            }

            private async ValueTask<ProjectionTargetMetadataCacheEntry> GetMetadataAsync(ProjectionTargetDescriptor<TProjectionId> target,
                                                                                         CancellationToken cancellation)
            {
                if (!_targetMetadataCache.TryGetValue(target, out var entry))
                {
                    var entryId = ProjectionTargetMetadata.GenerateId(target.TargetId.ToString(), StringifyType(target.TargetType));
                    var metadata = await _database.GetAsync<ProjectionTargetMetadata>(p => p.Id == entryId, cancellation).FirstOrDefault();
                    var originalMetadata = metadata;
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

                    entry = new ProjectionTargetMetadataCacheEntry(originalMetadata, metadata, touched);
                    _targetMetadataCache.Add(target, entry);
                }

                return entry;
            }

            private async ValueTask<ProjectionTargetMetadataCacheEntry> GetMetadataAsync(ProjectionTargetDescriptor target,
                                                                                         CancellationToken cancellation)
            {
                if (!_targetMetadataCache.TryGetValue(target, out var entry))
                {
                    var entryId = ProjectionTargetMetadata.GenerateId(target.TargetId, StringifyType(target.TargetType));
                    var metadata = await _database.GetAsync<ProjectionTargetMetadata>(p => p.Id == entryId, cancellation).FirstOrDefault();

                    entry = new ProjectionTargetMetadataCacheEntry(metadata, metadata, touched: false);

                    if (metadata != null)
                    {
                        _targetMetadataCache.Add(target, entry);
                    }
                }

                return entry;
            }

            private sealed class ProjectionTargetMetadata
            {
                private string _id;
                private string _stringifiedTargetId;

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
                            _id = GenerateId(StringifiedTargetId, TargetType);
                        }

                        return _id;
                    }
                    set => _id = value;
                }

                public TProjectionId TargetId { get; set; }
                public string StringifiedTargetId
                {
                    get
                    {
                        if (_stringifiedTargetId == null)
                        {
                            _stringifiedTargetId = TargetId.ToString();
                        }

                        return _stringifiedTargetId;
                    }
                    set => _stringifiedTargetId = value;
                }

                public string TargetType { get; set; }
                public List<ProjectionSource> ProjectionSources { get; } = new List<ProjectionSource>();
            }

            private sealed class ProjectionSource
            {
                public string Id { get; set; }
                public string Type { get; set; }
            }
        }

        #endregion

        #region Descriptors

        private readonly struct ProjectionSourceDescriptor : IEquatable<ProjectionSourceDescriptor>
        {
            public ProjectionSourceDescriptor(Type sourceType, string sourceId)
            {
                if (sourceType == null)
                    throw new ArgumentNullException(nameof(sourceType));

                if (sourceId == null || sourceId.Equals(default))
                    throw new ArgumentDefaultException(nameof(sourceId));

                SourceType = sourceType;
                SourceId = sourceId;
            }

            public Type SourceType { get; }
            public string SourceId { get; }

            public override bool Equals(object obj)
            {
                return obj is ProjectionSourceDescriptor entityDescriptor && Equals(entityDescriptor);
            }

            public bool Equals(ProjectionSourceDescriptor other)
            {
                return other.SourceType == null && SourceType == null || other.SourceType == SourceType && other.SourceId.Equals(SourceId);
            }

            public override int GetHashCode()
            {
                if (SourceType == null)
                    return 0;

                return SourceType.GetHashCode() ^ SourceId.GetHashCode();
            }

            public static bool operator ==(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
            {
                return !left.Equals(right);
            }
        }

        private readonly struct ProjectionTargetDescriptor : IEquatable<ProjectionTargetDescriptor>
        {
            public ProjectionTargetDescriptor(Type targetType, string targetId)
            {
                if (targetType == null)
                    throw new ArgumentNullException(nameof(targetType));

                if (targetId == null || targetId.Equals(default))
                    throw new ArgumentDefaultException(nameof(targetId));

                TargetType = targetType;
                TargetId = targetId;
            }

            public Type TargetType { get; }
            public string TargetId { get; }

            public override bool Equals(object obj)
            {
                return obj is ProjectionTargetDescriptor entityDescriptor && Equals(entityDescriptor);
            }

            public bool Equals(ProjectionTargetDescriptor other)
            {
                return other.TargetType == null && TargetType == null || other.TargetType == TargetType && other.TargetId.Equals(TargetId);
            }

            public override int GetHashCode()
            {
                if (TargetType == null)
                    return 0;

                return TargetType.GetHashCode() ^ TargetId.GetHashCode();
            }

            public static bool operator ==(in ProjectionTargetDescriptor left, in ProjectionTargetDescriptor right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(in ProjectionTargetDescriptor left, in ProjectionTargetDescriptor right)
            {
                return !left.Equals(right);
            }
        }

        private readonly struct ProjectionTargetDescriptor<TId> : IEquatable<ProjectionTargetDescriptor<TId>>
        {
            public ProjectionTargetDescriptor(Type targetType, TId targetId)
            {
                if (targetType == null)
                    throw new ArgumentNullException(nameof(targetType));

                if (targetId == null || targetId.Equals(default))
                    throw new ArgumentDefaultException(nameof(targetId));

                TargetType = targetType;
                TargetId = targetId;
            }

            public Type TargetType { get; }
            public TId TargetId { get; }
            public string StringifiedTargetId => TargetId.ToString();

            public override bool Equals(object obj)
            {
                return obj is ProjectionTargetDescriptor<TId> entityDescriptor && Equals(entityDescriptor);
            }

            public bool Equals(ProjectionTargetDescriptor<TId> other)
            {
                return other.TargetType == null && TargetType == null || other.TargetType == TargetType && other.TargetId.Equals(TargetId);
            }

            public override int GetHashCode()
            {
                if (TargetType == null)
                    return 0;

                return TargetType.GetHashCode() ^ TargetId.GetHashCode();
            }

            public static bool operator ==(in ProjectionTargetDescriptor<TId> left, in ProjectionTargetDescriptor<TId> right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(in ProjectionTargetDescriptor<TId> left, in ProjectionTargetDescriptor<TId> right)
            {
                return !left.Equals(right);
            }

            public static implicit operator ProjectionTargetDescriptor(in ProjectionTargetDescriptor<TId> typedDescriptor)
            {
                return new ProjectionTargetDescriptor(typedDescriptor.TargetType, typedDescriptor.StringifiedTargetId);
            }
        }

        #endregion
    }
}
