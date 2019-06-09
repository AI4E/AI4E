/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Projection
{
    // A projection engine that is scoped in the projection source (type and id specified via ProjectionSourceDescriptor)
    internal readonly struct SourceProjector
    {
        private readonly ProjectionSourceDescriptor _sourceDescriptor;
        private readonly IProjectionExecutor _projector;
        private readonly IDatabase _database;
        private readonly IServiceProvider _serviceProvider;

        private readonly ProjectionTargetProcessor _targetScopedProjectionEngine;

        private readonly SourceMetadataCache _metadataCache;

        public SourceProjector(
            in ProjectionSourceDescriptor sourceDescriptor,
            IProjectionExecutor projector,
            IDatabase database,
            IServiceProvider serviceProvider)
        {
            Debug.Assert(sourceDescriptor != default);
            Debug.Assert(projector != null);
            Debug.Assert(sourceDescriptor != default);
            Debug.Assert(database != null);
            Debug.Assert(serviceProvider != null);

            _sourceDescriptor = sourceDescriptor;
            _projector = projector;
            _database = database;
            _serviceProvider = serviceProvider;

            _metadataCache = new SourceMetadataCache(_sourceDescriptor, _database);

            _targetScopedProjectionEngine = new ProjectionTargetProcessor(_database);
        }

        // Projects the source entity and returns the descriptors of all dependent source entities. 
        public async Task<IEnumerable<ProjectionSourceDescriptor>> ProjectAsync(CancellationToken cancellation)
        {
            IEnumerable<ProjectionSourceDescriptor> dependents;
            using var scopedDatabase = _database.CreateScope();

            do
            {
                await ProjectCoreAsync(cancellation);
                dependents = await _metadataCache.GetDependentsAsync(cancellation);
            }
            while (!await WriteToDatabaseAsync(scopedDatabase, cancellation) || !await scopedDatabase.TryCommitAsync(cancellation));

            return dependents;
        }

        private async Task ProjectCoreAsync(CancellationToken cancellation)
        {
            _metadataCache.Clear();
            _targetScopedProjectionEngine.Clear();

            using var scope = _serviceProvider.CreateScope();
            var scopedServiceProvider = scope.ServiceProvider;
            var projectionSourceLoader = scopedServiceProvider.GetRequiredService<IProjectionSourceLoader>();
            var updateNeeded = await CheckUpdateNeededAsync(projectionSourceLoader, cancellation);

            if (!updateNeeded)
                return;

            var source = await projectionSourceLoader.GetSourceAsync(_sourceDescriptor, bypassCache: false, cancellation);
            var sourceRevision = await projectionSourceLoader.GetSourceRevisionAsync(_sourceDescriptor, bypassCache: false, cancellation);

            var projectionResults = _projector.ExecuteProjectionAsync(_sourceDescriptor.SourceType, source, scopedServiceProvider, cancellation);

            var metadata = await _metadataCache.GetMetadataAsync(cancellation);
            var appliedTargets = metadata.Targets.ToHashSet();

            var targets = new List<ProjectionTargetDescriptor>();

            // TODO: Ensure that there are no two projection results with the same type and id. 
            //       Otherwise bad things happen.

            await foreach (var projectionResult in projectionResults)
            {
                var projection = projectionResult.ToTargetDescriptor();
                targets.Add(projection);

                // The target was not part of the last projection. Store ourself to the target metadata.
                var addEntityToProjections = !appliedTargets.Remove(projection);

                await UpdateEntityToProjectionAsync(projectionResult, addEntityToProjections, cancellation);
            }

            // We removed all targets from 'applied projections' that are still present. 
            // The remaining ones are removed targets.
            foreach (var removedProjection in appliedTargets)
            {
                await RemoveEntityFromProjectionAsync(removedProjection, cancellation);
            }

            var dependencies = GetDependencies(projectionSourceLoader);

            var updatedMetadata = new SourceMetadata(dependencies, targets, sourceRevision);
            await _metadataCache.UpdateAsync(updatedMetadata, cancellation);
        }

        private async Task<bool> WriteToDatabaseAsync(IScopedDatabase scopedDatabase, CancellationToken cancellation)
        {
            // Write touched source metadata to database
            if (!await _metadataCache.CommitAsync(scopedDatabase, cancellation))
            {
                await scopedDatabase.RollbackAsync();
                return false;
            }

            if (!await _targetScopedProjectionEngine.WriteToDatabaseAsync(scopedDatabase, cancellation))
            {
                await scopedDatabase.RollbackAsync();
                return false;
            }

            return true;
        }

        // Checks whether the projection is up-to date our if we have to reproject.
        // We have to project if
        // - the version of our entity is greater than the projection version
        // - the version of any of our dependencies is greater than the projection version
        private async ValueTask<bool> CheckUpdateNeededAsync(
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            // We load all dependencies from the entity store. 
            // As a projection source's dependencies do not change often, chances are good that the 
            // entities are cached when accessed during the projection phase.
            // For that reason, performance should not suffer very much 
            // in comparison to not checking whether an update is needed.

            var metadata = await _metadataCache.GetMetadataAsync(cancellation);

            if (metadata.ProjectionRevision == 0)
            {
                return true;
            }

            return await CheckUpdateNeededAsync(metadata, projectionSourceLoader, cancellation);
        }

        private async ValueTask<bool> CheckUpdateNeededAsync(
            SourceMetadata metadata,
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            var sourceRevision = await GetSourceRevisionAsync(_sourceDescriptor, metadata.ProjectionRevision, projectionSourceLoader, cancellation);
            Debug.Assert(sourceRevision >= metadata.ProjectionRevision);

            if (sourceRevision > metadata.ProjectionRevision)
            {
                return true;
            }

            foreach (var (dependency, dependencyProjectionRevision) in metadata.Dependencies)
            {
                var dependencyRevision = await GetSourceRevisionAsync(_sourceDescriptor, metadata.ProjectionRevision, projectionSourceLoader, cancellation);
                Debug.Assert(dependencyRevision >= dependencyProjectionRevision);

                if (dependencyRevision > dependencyProjectionRevision)
                {
                    return true;
                }
            }

            return false;
        }

        private async ValueTask<long> GetSourceRevisionAsync(
            ProjectionSourceDescriptor projectionSource,
            long projectionRevision,
            IProjectionSourceLoader projectionSourceLoader,
            CancellationToken cancellation)
        {
            long sourceRevision;
            var sourceOutOfDate = false;

            do
            {
                sourceRevision = await projectionSourceLoader.GetSourceRevisionAsync(
                    projectionSource, bypassCache: sourceOutOfDate, cancellation);

                sourceOutOfDate = sourceRevision < projectionRevision;

            } while (sourceRevision < projectionRevision);

            return sourceRevision;
        }

        // Gets our dependencies from the entity store.
        private IEnumerable<ProjectionSourceDependency> GetDependencies(IProjectionSourceLoader projectionSourceLoader)
        {
            var sourceDescriptor = _sourceDescriptor;
            return projectionSourceLoader.LoadedSources.Where(p => p.Dependency != sourceDescriptor).ToArray();
        }

        #region ProjectCore

        private Task RemoveEntityFromProjectionAsync(ProjectionTargetDescriptor removedProjection,
                                                     CancellationToken cancellation)
        {
            return _targetScopedProjectionEngine.RemoveEntityFromProjectionAsync(_sourceDescriptor, removedProjection, cancellation);
        }

        private Task UpdateEntityToProjectionAsync(IProjectionResult projectionResult,
                                                bool addEntityToProjections,
                                                CancellationToken cancellation)
        {
            return _targetScopedProjectionEngine.UpdateEntityToProjectionAsync(_sourceDescriptor, projectionResult, addEntityToProjections, cancellation);
        }

        #endregion

        private sealed class ProjectionTargetProcessor
        {
            private static readonly MethodInfo _loadTargetMethodDefinition;

            private static readonly ConcurrentDictionary<Type, Func<IDatabase, object, CancellationToken, ValueTask<object>>> _loadTargetMethods
                           = new ConcurrentDictionary<Type, Func<IDatabase, object, CancellationToken, ValueTask<object>>>();

            private static readonly Func<Type, Func<IDatabase, object, CancellationToken, ValueTask<object>>> _buildLoadTargetMethodCache = BuildLoadTargetMethod;

            static ProjectionTargetProcessor()
            {
                _loadTargetMethodDefinition = typeof(ProjectionTargetProcessor)
                    .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Single(p => p.Name == nameof(ProjectionTargetProcessor.LoadTargetAsync) &&
                                 p.IsGenericMethodDefinition);
            }

            private readonly IDatabase _database;
            private readonly IDictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry> _targetMetadataCache;
            private readonly Dictionary<ProjectionTargetDescriptor, object> _targetsToUpdate = new Dictionary<ProjectionTargetDescriptor, object>();
            private readonly Dictionary<ProjectionTargetDescriptor, object> _targetsToDelete = new Dictionary<ProjectionTargetDescriptor, object>();

            public ProjectionTargetProcessor(IDatabase database)
            {
                _targetMetadataCache = new Dictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry>();
                _database = database;
            }

            public void Clear()
            {
                _targetMetadataCache.Clear();
                _targetsToUpdate.Clear();
                _targetsToDelete.Clear();
            }

            public async Task<bool> WriteToDatabaseAsync(IScopedDatabase transactionalDatabase, CancellationToken cancellation)
            {
                // Write touched target metadata to database
                foreach (var (originalMetadata, touchedMetadata) in _targetMetadataCache.Values.Where(p => p.Touched))
                {
                    var comparandMetdata = await transactionalDatabase
                        .GetAsync<ProjectionTargetMetadataEntry>(p => p.Id == (originalMetadata ?? touchedMetadata).Id)
                        .FirstOrDefaultAsync(cancellation);

                    if (!ProjectionTargetMetadataEntry.MatchesByRevision(originalMetadata, comparandMetdata))
                    {
                        return false;
                    }

                    if (touchedMetadata == null)
                    {
                        Debug.Assert(originalMetadata != null);

                        await transactionalDatabase.RemoveAsync(originalMetadata, cancellation);
                    }
                    else
                    {
                        touchedMetadata.MetadataRevision = originalMetadata?.MetadataRevision ?? 1;

                        await transactionalDatabase.StoreAsync(touchedMetadata, cancellation);
                    }
                }

                // TODO: Do we have to check whether the targets were updated concurrently?

                foreach (var target in _targetsToUpdate)
                {
                    await transactionalDatabase.StoreAsync(target.Key.TargetType, target.Value, cancellation);
                }

                foreach (var target in _targetsToDelete)
                {
                    await transactionalDatabase.RemoveAsync(target.Key.TargetType, target.Value, cancellation);
                }

                return true;
            }

            public async Task UpdateEntityToProjectionAsync(
                ProjectionSourceDescriptor source,
                IProjectionResult projectionResult,
                bool addToTargetMetadata,
                CancellationToken cancellation)
            {
                var target = new ProjectionTargetDescriptor(projectionResult.ResultType, projectionResult.ResultId.ToString()); // TODO: Can the id be null?
                _targetsToUpdate[target] = projectionResult.Result;

                if (!addToTargetMetadata)
                {
                    return;
                }

                var entry = await GetEntryAsync(target, cancellation) ?? new ProjectionTargetMetadataEntry
                {
                    TargetId = projectionResult.ResultId,
                    TargetType = projectionResult.ResultType.GetUnqualifiedTypeName()
                };

                entry.ProjectionSources.Add(new ProjectionSourceEntry(source));

                UpdateEntry(entry);
            }

            public async Task RemoveEntityFromProjectionAsync(ProjectionSourceDescriptor projectionSource,
                                                              ProjectionTargetDescriptor removedProjection,
                                                              CancellationToken cancellation)
            {
                var entry = await GetEntryAsync(removedProjection, cancellation);

                if (entry == null)
                {
                    return;
                }

                var removed = entry.ProjectionSources
                                      .RemoveFirstWhere(p => p.Id == projectionSource.SourceId &&
                                                             p.Type == projectionSource.SourceType.GetUnqualifiedTypeName());

                if (!entry.ProjectionSources.Any())
                {
                    DeleteEntry(entry);

                    object projection = LoadTargetAsync(_database, removedProjection.TargetType, entry.TargetId, cancellation);

                    if (projection != null)
                    {
                        _targetsToDelete[removedProjection] = projection;
                    }
                }
                else
                {
                    UpdateEntry(entry);
                }
            }

            private static Func<IDatabase, object, CancellationToken, ValueTask<object>> BuildLoadTargetMethod(Type targetType)
            {
                Debug.Assert(_loadTargetMethodDefinition.IsGenericMethodDefinition);
                Debug.Assert(_loadTargetMethodDefinition.GetGenericArguments().Length == 2);

                var idType = DataPropertyHelper.GetIdType(targetType);
                var method = _loadTargetMethodDefinition.MakeGenericMethod(idType, targetType);

                Debug.Assert(method.ReturnType == typeof(ValueTask<object>));
                Debug.Assert(method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new Type[] { typeof(IDatabase), idType, typeof(CancellationToken) }));

                var databaseParameter = Expression.Parameter(typeof(IDatabase), "database");
                var idParameter = Expression.Parameter(typeof(object), "id");
                var cancellationParameter = Expression.Parameter(typeof(CancellationToken), "cancellation");
                var convertedId = Expression.Convert(idParameter, idType);
                var call = Expression.Call(method, databaseParameter, convertedId, cancellationParameter);
                var lambda = Expression.Lambda<Func<IDatabase, object, CancellationToken, ValueTask<object>>>(call, databaseParameter, idParameter, cancellationParameter);
                return lambda.Compile();
            }

            private ValueTask<object> LoadTargetAsync(IDatabase database, Type targetType, object id, CancellationToken cancellation)
            {
                var invoker = _loadTargetMethods.GetOrAdd(targetType, _buildLoadTargetMethodCache);
                return invoker.Invoke(database, id, cancellation);
            }

            private static async ValueTask<object> LoadTargetAsync<TId, TTarget>(IDatabase database, TId id, CancellationToken cancellation)
                where TTarget : class
            {
                var predicate = DataPropertyHelper.BuildPredicate<TId, TTarget>(id);
                return await database.GetOneAsync(predicate, cancellation);
            }

            private readonly struct ProjectionTargetMetadataCacheEntry
            {
                public ProjectionTargetMetadataCacheEntry(ProjectionTargetMetadataEntry originalEntry,
                                                          ProjectionTargetMetadataEntry entry,
                                                          bool touched)
                {
                    Debug.Assert(originalEntry != null || entry != null);

                    OriginalEntry = originalEntry;
                    Entry = entry;
                    Touched = touched;
                }

                public ProjectionTargetMetadataEntry OriginalEntry { get; }
                public ProjectionTargetMetadataEntry Entry { get; }
                public bool Touched { get; }

                public void Deconstruct(out ProjectionTargetMetadataEntry originalMetadata,
                                        out ProjectionTargetMetadataEntry metadata)
                {
                    originalMetadata = OriginalEntry;
                    metadata = Entry;
                }
            }

            private void UpdateEntry(ProjectionTargetMetadataEntry entry)
            {
                Debug.Assert(entry != null);

                var targetType = entry.TargetType;
                var targetId = entry.StringifiedTargetId;
                var targetDescriptor = new ProjectionTargetDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(targetType), targetId);

                if (!_targetMetadataCache.TryGetValue(targetDescriptor, out var cacheEntry))
                {
                    cacheEntry = default;
                }

                _targetMetadataCache[targetDescriptor] = new ProjectionTargetMetadataCacheEntry(cacheEntry.OriginalEntry, entry.DeepClone(), touched: true);
            }

            private void DeleteEntry(ProjectionTargetMetadataEntry entry)
            {
                Debug.Assert(entry != null);

                var targetType = entry.TargetType;
                var targetId = entry.StringifiedTargetId;
                var targetDescriptor = new ProjectionTargetDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(targetType), targetId);

                if (!_targetMetadataCache.TryGetValue(targetDescriptor, out var cacheEntry))
                {
                    return;
                }

                if (cacheEntry.OriginalEntry is null)
                {
                    _targetMetadataCache.Remove(targetDescriptor);
                }

                _targetMetadataCache[targetDescriptor] = new ProjectionTargetMetadataCacheEntry(cacheEntry.OriginalEntry, null, touched: true);
            }

            private ValueTask<ProjectionTargetMetadataEntry> GetEntryAsync(
                ProjectionTargetDescriptor target,
                CancellationToken cancellation)
            {
                if (_targetMetadataCache.TryGetValue(target, out var cacheEntry))
                {
                    return new ValueTask<ProjectionTargetMetadataEntry>(cacheEntry.Entry.DeepClone());
                }

                return GetEntryCoreAsync(target, cancellation);
            }

            private async ValueTask<ProjectionTargetMetadataEntry> GetEntryCoreAsync(
                ProjectionTargetDescriptor target,
                CancellationToken cancellation)
            {
                var entryId = ProjectionTargetMetadataEntry.GenerateId(
                    target.TargetId,
                    target.TargetType.GetUnqualifiedTypeName());

                var entry = await _database.GetOneAsync<ProjectionTargetMetadataEntry>(p => p.Id == entryId, cancellation);

                if (entry != null)
                {
                    var originalEntry = entry.DeepClone();
                    var cacheEntry = new ProjectionTargetMetadataCacheEntry(originalEntry, entry, touched: false);
                    _targetMetadataCache.Add(target, cacheEntry);
                }

                return entry;
            }

            private sealed class ProjectionTargetMetadataEntry
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

                public long MetadataRevision { get; set; } = 1;

                public object TargetId { get; set; }
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
                public List<ProjectionSourceEntry> ProjectionSources { get; private set; } = new List<ProjectionSourceEntry>();

                public static bool MatchesByRevision(ProjectionTargetMetadataEntry original, ProjectionTargetMetadataEntry comparand)
                {
                    if (original is null)
                        return comparand is null;

                    if (comparand is null)
                        return false;

                    return original.MetadataRevision == comparand.MetadataRevision;
                }
            }

            private sealed class ProjectionSourceEntry
            {
                public ProjectionSourceEntry() { }

                public ProjectionSourceEntry(ProjectionSourceDescriptor source)
                {
                    Id = source.SourceId;
                    Type = source.SourceType.ToString();
                }

                public string Id { get; set; }
                public string Type { get; set; }
            }
        }
    }
}
