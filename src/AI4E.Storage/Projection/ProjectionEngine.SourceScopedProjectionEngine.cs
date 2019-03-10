using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Transactions;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Projection
{
    public sealed partial class ProjectionEngine
    {
        // A projection engine that is scoped in the projection source (type and id specified via ProjectionSourceDescriptor)
        private readonly struct SourceScopedProjectionEngine
        {
            private readonly ProjectionSourceDescriptor _sourceDescriptor;
            private readonly IProjector _projector;
            private readonly ITransactionalDatabase _transactionalDatabase;
            private readonly IDatabase _database;
            private readonly IServiceProvider _serviceProvider;

            private readonly IDictionary<ProjectionSourceDescriptor, ProjectionSourceMetadataCacheEntry> _sourceMetadataCache;
            private readonly IDictionary<Type, ITargetScopedProjectionEngine> _targetScopedProjectionEngines;

            public SourceScopedProjectionEngine(in ProjectionSourceDescriptor sourceDescriptor,
                                                IProjector projector,
                                                ITransactionalDatabase transactionalDatabase,
                                                IDatabase database,
                                                IServiceProvider serviceProvider)
            {
                Assert(sourceDescriptor != default);
                Assert(projector != null);
                Assert(sourceDescriptor != default);
                Assert(transactionalDatabase != null);
                Assert(database != null);
                Assert(serviceProvider != null);

                _sourceDescriptor = sourceDescriptor;
                _projector = projector;
                _transactionalDatabase = transactionalDatabase;
                _database = database;
                _serviceProvider = serviceProvider;

                _sourceMetadataCache = new Dictionary<ProjectionSourceDescriptor, ProjectionSourceMetadataCacheEntry>();
                _targetScopedProjectionEngines = new Dictionary<Type, ITargetScopedProjectionEngine>();
            }


            // Projects the source entity and returns the descriptors of all dependent source entities. 
            public async Task<IEnumerable<ProjectionSourceDescriptor>> ProjectAsync(CancellationToken cancellation)
            {
                using (var transactionalDatabase = _transactionalDatabase.CreateScope())
                {
                    do
                    {
                        do
                        {
                            _sourceMetadataCache.Clear();
                            _targetScopedProjectionEngines.Clear();

                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var scopedServiceProvider = scope.ServiceProvider;
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
                                    await UpdateDependenciesAsync(dependencies, cancellation); // TODO: Do we have to clear the dependency list, if there are no projections?
                                }

                                break;
                            }
                        }
                        while (true);

                        var dependents = await GetDependentsAsync(cancellation);

                        try
                        {
                            if (!await WriteToDatabaseAsync(transactionalDatabase, cancellation))
                            {
                                await transactionalDatabase.RollbackAsync();
                                continue;
                            }

                            if (await transactionalDatabase.TryCommitAsync(cancellation))
                            {
                                return dependents;
                            }
                        }
                        catch (TransactionAbortedException) { }
                        catch
                        {
                            await transactionalDatabase.RollbackAsync();
                            throw;
                        }
                    }
                    while (true);
                }
            }

            private async Task<bool> WriteToDatabaseAsync(IScopedTransactionalDatabase transactionalDatabase, CancellationToken cancellation)
            {
                // Write touched source metadata to database
                foreach (var (originalMetadata, touchedMetadata) in _sourceMetadataCache.Values.Where(p => p.Touched))
                {
                    var comparandMetdata = await transactionalDatabase.GetAsync<ProjectionSourceMetadata>(p => p.Id == (originalMetadata ?? touchedMetadata).Id).FirstOrDefault();

                    if (!MatchesByRevision(originalMetadata, comparandMetdata))
                    {
                        return false;
                    }

                    if (touchedMetadata == null)
                    {
                        Assert(originalMetadata != null);

                        await transactionalDatabase.RemoveAsync(originalMetadata, cancellation);
                    }
                    else
                    {
                        touchedMetadata.MetadataRevision = originalMetadata?.MetadataRevision ?? 1;

                        await transactionalDatabase.StoreAsync(touchedMetadata, cancellation);
                    }
                }

                foreach (var targetTypedScopedEngine in _targetScopedProjectionEngines.Values)
                {
                    if (!await targetTypedScopedEngine.WriteToDatabaseAsync(transactionalDatabase, cancellation))
                    {
                        return false;
                    }
                }

                return true;
            }

            private bool MatchesByRevision(ProjectionSourceMetadata original, ProjectionSourceMetadata comparand)
            {
                if (original is null)
                    return comparand is null;

                if (comparand is null)
                    return false;

                return original.MetadataRevision == comparand.MetadataRevision;
            }

            // Checks whether the projection is up-to date our if we have to reproject.
            // We have to project if
            // - the version of our entity is greater than the projection version
            // - the version of any of our dependencies is greater than the projection version
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

            // Gets our dependencies from the entity store.
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

            // Gets our dependents.
            private async ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation)
            {
                var (_, metadata) = await GetMetadataAsync(createIfNonExistent: false, cancellation);

                if (metadata == null)
                {
                    return Enumerable.Empty<ProjectionSourceDescriptor>();
                }

                return metadata.Dependents.Select(p => new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(p.Type), p.Id));
            }

            // Updates our dependency list to match the specified dependencies.
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
                        Type = p.dependency.SourceType.GetUnqualifiedTypeName(),
                        ProjectionRevision = p.revision
                    };
                }

                metadata.Dependencies.Clear();
                metadata.Dependencies.AddRange(dependencies.Select(p => ToStoredDependency(p)));

                _sourceMetadataCache[_sourceDescriptor] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
            }

            // Add ourself as dependent to `dependency`.
            private async Task AddDependentAsync(ProjectionSourceDescriptor dependency,
                                                 CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var (originalMetadata, metadata) = await GetMetadataAsync(dependency, createIfNonExistent: true, cancellation);
                var dependent = new Dependent
                {
                    Id = _sourceDescriptor.SourceId,
                    Type = _sourceDescriptor.SourceType.GetUnqualifiedTypeName()
                };

                metadata.Dependents.Add(dependent);
                _sourceMetadataCache[dependency] = new ProjectionSourceMetadataCacheEntry(originalMetadata, metadata, touched: true);
            }

            // Remove ourself as dependent from `dependency`.
            private async Task RemoveDependentAsync(ProjectionSourceDescriptor dependency,
                                                    CancellationToken cancellation)
            {
                if (dependency == default)
                    throw new ArgumentDefaultException(nameof(dependency));

                var entityDescriptor = _sourceDescriptor;

                var (originalMetadata, metadata) = await GetMetadataAsync(dependency, createIfNonExistent: true, cancellation);
                var removed = metadata.Dependents.RemoveFirstWhere(p => p.Id.Equals(entityDescriptor.SourceId) &&
                                                                        p.Type == entityDescriptor.SourceType.GetUnqualifiedTypeName());

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
                var projectionResults = _projector.ProjectAsync(entityType, entity, serviceProvider, cancellation);
                var appliedProjections = new HashSet<ProjectionTargetDescriptor>(await GetAppliedProjectionsAsync(cancellation));
                var projections = new List<ProjectionTargetDescriptor>();


                // TODO: Ensure that there are no two projection results with the same type and id. 
                //       Otherwise bad things happen.
                var projectionsPresent = false;
                IAsyncEnumerator<IProjectionResult> projectionResultsEnumerator = null;

                try
                {
                    projectionResultsEnumerator = projectionResults.GetEnumerator();

                    while (await projectionResultsEnumerator.MoveNext(cancellation))
                    {
                        projectionsPresent = true;
                        var projectionResult = projectionResultsEnumerator.Current;
                        var projection = new ProjectionTargetDescriptor(projectionResult.ResultType,
                                                                        projectionResult.ResultId.ToString());
                        projections.Add(projection);

                        // The target was not part of the last projection. Store ourself to the target metadata.
                        var addEntityToProjections = !appliedProjections.Remove(projection);

                        //if (!appliedProjections.Remove(projection))
                        //{
                        await UpdateEntityToProjectionAsync(projectionResult, addEntityToProjections, cancellation);
                        //}
                        //await _database.StoreAsync(projectionResult.ResultType, projectionResult.Result, cancellation);
                    }
                }
                finally
                {
                    projectionResultsEnumerator?.Dispose();
                }

                // We removed all current projections from applied projections. 
                // The remaining ones are removed projections.
                foreach (var removedProjection in appliedProjections)
                {
                    await RemoveEntityFromProjectionAsync(removedProjection, cancellation);
                }

                // Update the projection's metadata in the database
                await UpdateAppliedProjectionsAsync(projections, cancellation);

                return projectionsPresent;
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

            private Task UpdateEntityToProjectionAsync(IProjectionResult projectionResult,
                                                    bool addEntityToProjections,
                                                    CancellationToken cancellation)
            {
                var targetScopedProjectionEngine = GetTargetScopedProjectionEngine(projectionResult.ResultType);

                return targetScopedProjectionEngine.UpdateEntityToProjectionAsync(_sourceDescriptor, projectionResult, addEntityToProjections, cancellation);
            }

            // Gets the projection targets that are currently in the db.
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
                metadata.ProjectionTargets.AddRange(projections.Select(p => new ProjectionTarget { Type = p.TargetType.GetUnqualifiedTypeName(), Id = p.TargetId }));
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
                    var entryId = ProjectionSourceMetadata.GenerateId(sourceDescriptor.SourceId, sourceDescriptor.SourceType.GetUnqualifiedTypeName());
                    var metadata = await _database.GetAsync<ProjectionSourceMetadata>(p => p.Id == entryId, cancellation).FirstOrDefault(cancellation);

                    var originalMetadata = metadata;
                    var touched = false;

                    if (metadata == null && createIfNonExistent)
                    {
                        metadata = new ProjectionSourceMetadata
                        {
                            SourceId = sourceDescriptor.SourceId,
                            SourceType = sourceDescriptor.SourceType.GetUnqualifiedTypeName()
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
                        SourceType = sourceDescriptor.SourceType.GetUnqualifiedTypeName()
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

                public long MetadataRevision { get; set; } = 1;

                public string SourceId { get; set; }
                public string SourceType { get; set; }
                public long ProjectionRevision { get; set; }
                public List<ProjectionTarget> ProjectionTargets { get; private set; } = new List<ProjectionTarget>();
                public List<Dependency> Dependencies { get; private set; } = new List<Dependency>();
                public List<Dependent> Dependents { get; private set; } = new List<Dependent>();
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
    }
}
