/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection target processor, that stored the targets in a database.
    /// </summary>
    public sealed class ProjectionTargetProcessor : IProjectionTargetProcessor
    {
        #region Fields

        private readonly IDatabase _database;

        private readonly MetadataCache<ProjectionSourceDescriptor, ProjectionSourceMetadataEntry> _sourceMetadataCache;
        private readonly MetadataCache<ProjectionTargetDescriptor, ProjectionTargetMetadataEntry> _targetMetadataCache;

        private readonly Dictionary<ProjectionTargetDescriptor, object> _targetsToUpdate
            = new Dictionary<ProjectionTargetDescriptor, object>();
        private readonly Dictionary<ProjectionTargetDescriptor, object> _targetsToDelete
            = new Dictionary<ProjectionTargetDescriptor, object>();

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionTargetProcessor"/> type.
        /// </summary>
        /// <param name="projectedSource">A descriptor for the projected source.</param>
        /// <param name="database">The database that the targets and metadata shall be stored in.</param>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="projectedSource"/> is <c>default</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="database"/> is <c>null</c>.</exception>
        public ProjectionTargetProcessor(ProjectionSourceDescriptor projectedSource, IDatabase database)
        {
            if (projectedSource == default)
                throw new ArgumentDefaultException(nameof(projectedSource));

            if (database is null)
                throw new ArgumentNullException(nameof(database));

            _sourceMetadataCache = new MetadataCache<ProjectionSourceDescriptor, ProjectionSourceMetadataEntry>(
                database,
                ProjectionSourceMetadataEntry.GetDescriptor,
                BuildQuery);

            _targetMetadataCache = new MetadataCache<ProjectionTargetDescriptor, ProjectionTargetMetadataEntry>(
                database,
                ProjectionTargetMetadataEntry.GetDescriptor,
                BuildQuery);

            ProjectedSource = projectedSource;
            _database = database;
        }

        private static Expression<Func<ProjectionSourceMetadataEntry, bool>> BuildQuery(ProjectionSourceDescriptor source)
        {
            var entryId = ProjectionSourceMetadataEntry.GenerateId(
                source.SourceId,
                source.SourceType.GetUnqualifiedTypeName());

            return p => p.Id == entryId;
        }

        private static Expression<Func<ProjectionTargetMetadataEntry, bool>> BuildQuery(ProjectionTargetDescriptor target)
        {
            var entryId = ProjectionTargetMetadataEntry.GenerateId(
                target.TargetId,
                target.TargetType.GetUnqualifiedTypeName());

            return p => p.Id == entryId;
        }

        #endregion

        #region ISourceMetadataCache

        /// <inheritdoc/>
        public ProjectionSourceDescriptor ProjectedSource { get; }

        /// <inheritdoc/>
        public async ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation)
        {
            var entry = await _sourceMetadataCache.GetEntryAsync(ProjectedSource, cancellation).ConfigureAwait(false);

            return entry == null
                ? Enumerable.Empty<ProjectionSourceDescriptor>()
                : entry.Dependents.Select(p => p.ToDescriptor()).ToImmutableList();
        }

        /// <inheritdoc/>
        public async ValueTask<ProjectionMetadata> GetMetadataAsync(CancellationToken cancellation)
        {
            var entry = await _sourceMetadataCache.GetEntryAsync(ProjectedSource, cancellation).ConfigureAwait(false);

            if (entry == null)
                return default;

            return entry.ToSourceMetadata();
        }

        /// <inheritdoc/>
        public async ValueTask UpdateAsync(ProjectionMetadata metadata, CancellationToken cancellation)
        {
            var entry = await _sourceMetadataCache.GetEntryAsync(ProjectedSource, cancellation).ConfigureAwait(false) ??
                        new ProjectionSourceMetadataEntry(
                            ProjectedSource.SourceId,
                            ProjectedSource.SourceType.GetUnqualifiedTypeName());

            // Write the new source revision to the metadata
            entry.ProjectionRevision = metadata.ProjectionRevision;

            // Write the set of applied targets to the metadata
            entry.ProjectionTargets.Clear();
            if (metadata.Targets.Any())
            {
                entry.ProjectionTargets.AddRange(metadata.Targets.Select(p => new ProjectionTargetEntry(p)));
            }
            else if (!entry.Dependents.Any())
            {
                await _sourceMetadataCache.DeleteEntryAsync(entry, cancellation).ConfigureAwait(false);
                return;
            }

            // Write our dependencies to the metadata
            var storedDependencies = entry.Dependencies.Select(p => p.ToDescriptor().Dependency);

            foreach (var dependency in metadata.Dependencies.Select(p => p.Dependency).Except(storedDependencies))
            {
                // Add ourself as dependent to `dependency`.            
                var dependencyEntry = await _sourceMetadataCache
                    .GetEntryAsync(dependency, cancellation)
                    .ConfigureAwait(false)
                    ?? new ProjectionSourceMetadataEntry(
                        dependency.SourceId,
                        dependency.SourceType.GetUnqualifiedTypeName());

                dependencyEntry.Dependents.Add(new DependentEntry(ProjectedSource));
                await _sourceMetadataCache.UpdateEntryAsync(dependencyEntry, cancellation).ConfigureAwait(false);
            }

            foreach (var dependency in storedDependencies.Except(metadata.Dependencies.Select(p => p.Dependency)))
            {
                // Remove ourself as dependent from `dependency`.
                var dependencyEntry = await _sourceMetadataCache
                    .GetEntryAsync(dependency, cancellation)
                    .ConfigureAwait(false)
                    ?? new ProjectionSourceMetadataEntry(
                        dependency.SourceId,
                        dependency.SourceType.GetUnqualifiedTypeName());

                var removed = dependencyEntry.Dependents.Remove(new DependentEntry(ProjectedSource));

                Debug.Assert(removed);

                if (dependencyEntry.ProjectionTargets.Any() || dependencyEntry.Dependents.Any())
                {
                    await _sourceMetadataCache.UpdateEntryAsync(dependencyEntry, cancellation).ConfigureAwait(false);
                }
                else
                {
                    Debug.Assert(!dependencyEntry.Dependencies.Any());
                    await _sourceMetadataCache.DeleteEntryAsync(dependencyEntry, cancellation).ConfigureAwait(false);
                }
            }

            entry.Dependencies.Clear();
            entry.Dependencies.AddRange(metadata.Dependencies.Select(p => new DependencyEntry(p)));

            await _sourceMetadataCache.UpdateEntryAsync(entry, cancellation).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async ValueTask<bool> CommitAsync(CancellationToken cancellation)
        {
            using var scopedDatabase = _database.CreateScope();

            try
            {
                // Write touched source metadata to database
                foreach (var cacheEntry in _sourceMetadataCache.GetTrackedEntries().Where(
                    p => p.State != MetadataCacheEntryState.Unchanged))
                {
                    // Check whether there are concurrent changes on the metadata.
                    var comparandMetdata = await scopedDatabase
                        .GetAsync<ProjectionSourceMetadataEntry>(
                        p => p.Id == (cacheEntry.Entry ?? cacheEntry.OriginalEntry).Id)
                        .FirstOrDefaultAsync(cancellation)
                        .ConfigureAwait(false);

                    if (!ProjectionSourceMetadataEntry.MatchesByRevision(cacheEntry.OriginalEntry, comparandMetdata))
                    {
                        await scopedDatabase.RollbackAsync().ConfigureAwait(false);
                        return false;
                    }

                    if (cacheEntry.State == MetadataCacheEntryState.Created
                        || cacheEntry.State == MetadataCacheEntryState.Updated)
                    {
                        cacheEntry.Entry.MetadataRevision = (cacheEntry.OriginalEntry?.MetadataRevision ?? 0) + 1;
                        await scopedDatabase.StoreAsync(cacheEntry.Entry, cancellation).ConfigureAwait(false);
                    }
                    else
                    {
                        await scopedDatabase.RemoveAsync(cacheEntry.OriginalEntry, cancellation).ConfigureAwait(false);
                    }
                }

                // Write touched target metadata to database
                foreach (var cacheEntry in _targetMetadataCache.GetTrackedEntries().Where(
                    p => p.State != MetadataCacheEntryState.Unchanged))
                {
                    // Check whether there are concurrent changes on the metadata.
                    var comparandMetdata = await scopedDatabase
                        .GetAsync<ProjectionTargetMetadataEntry>(
                        p => p.Id == (cacheEntry.Entry ?? cacheEntry.OriginalEntry).Id)
                        .FirstOrDefaultAsync(cancellation).ConfigureAwait(false);

                    if (!ProjectionTargetMetadataEntry.MatchesByRevision(cacheEntry.OriginalEntry, comparandMetdata))
                    {
                        await scopedDatabase.RollbackAsync().ConfigureAwait(false);
                        return false;
                    }

                    if (cacheEntry.State == MetadataCacheEntryState.Created
                     || cacheEntry.State == MetadataCacheEntryState.Updated)
                    {
                        cacheEntry.Entry.MetadataRevision = (cacheEntry.OriginalEntry?.MetadataRevision ?? 0) + 1;
                        await scopedDatabase.StoreAsync(cacheEntry.Entry, cancellation).ConfigureAwait(false);
                    }
                    else
                    {
                        await scopedDatabase.RemoveAsync(cacheEntry.OriginalEntry, cancellation).ConfigureAwait(false);
                    }
                }

                // We do not need to check whether the targets were updated concurrently as
                // we are already checking the metadata for concurrent changes and the projection targets are
                // only updated/deleted in combination with the respective metadata transactionally.

                foreach (var target in _targetsToUpdate)
                {
                    await GetTypedTargetProcessor(target.Key.TargetType)
                        .StoreAsync(scopedDatabase, target.Value, cancellation)
                        .ConfigureAwait(false);
                }

                foreach (var target in _targetsToDelete)
                {
                    await GetTypedTargetProcessor(target.Key.TargetType)
                        .RemoveAsync(scopedDatabase, target.Value, cancellation)
                        .ConfigureAwait(false);
                }
            }
            catch
            {
                await scopedDatabase.RollbackAsync().ConfigureAwait(false);
                throw;
            }

            return await scopedDatabase.TryCommitAsync(cancellation).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _sourceMetadataCache.Clear();
            _targetMetadataCache.Clear();
            _targetsToUpdate.Clear();
            _targetsToDelete.Clear();
        }

        /// <inheritdoc/>
        public async ValueTask UpdateTargetAsync(
            IProjectionResult projectionResult,
            CancellationToken cancellation)
        {
            if (projectionResult is null)
                throw new ArgumentNullException(nameof(projectionResult));

            var target = new ProjectionTargetDescriptor(
                projectionResult.ResultType, projectionResult.ResultId.ToString());
            _targetsToUpdate[target] = projectionResult.Result;

            var entry = await _targetMetadataCache.GetEntryAsync(target, cancellation).ConfigureAwait(false)
                ?? new ProjectionTargetMetadataEntry
                {
                    TargetId = projectionResult.ResultId,
                    TargetType = projectionResult.ResultType.GetUnqualifiedTypeName()
                };

            if (!entry.ProjectionSources.Any(p => p.ToDescriptor() == ProjectedSource))
            {
                entry.ProjectionSources.Add(new ProjectionSourceEntry(ProjectedSource));
                await _targetMetadataCache.UpdateEntryAsync(entry, cancellation).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask RemoveTargetAsync(
            ProjectionTargetDescriptor target,
            CancellationToken cancellation)
        {
            if (target == default)
                throw new ArgumentDefaultException(nameof(target));

            var entry = await _targetMetadataCache.GetEntryAsync(target, cancellation).ConfigureAwait(false);

            if (entry == null)
            {
                return;
            }

            var removed = entry.ProjectionSources
                                  .RemoveFirstWhere(p => p.Id == ProjectedSource.SourceId &&
                                                         p.Type == ProjectedSource.SourceType.GetUnqualifiedTypeName());

            if (!entry.ProjectionSources.Any())
            {
                await _targetMetadataCache.DeleteEntryAsync(entry, cancellation).ConfigureAwait(false);

                var projection = await LoadTargetAsync(_database, target.TargetType, entry.TargetId, cancellation)
                    .ConfigureAwait(false);

                if (projection != null)
                {
                    _targetsToDelete[target] = projection;
                }
            }
            else
            {
                await _targetMetadataCache.UpdateEntryAsync(entry, cancellation).ConfigureAwait(false);
            }
        }

        #endregion

        #region LoadTarget

        // TODO: If we can create a predicate from an object typed id, we can remove all of this and implement the stub in TypedTargetProcessor<TTarget>

        private static readonly MethodInfo _loadTargetMethodDefinition;

        private static readonly ConcurrentDictionary<Type, Func<IDatabase, object, CancellationToken, ValueTask<object>>> _loadTargetMethods
            = new ConcurrentDictionary<Type, Func<IDatabase, object, CancellationToken, ValueTask<object>>>();

        private static readonly Func<Type, Func<IDatabase, object, CancellationToken, ValueTask<object>>> _buildLoadTargetMethodCache
            = BuildLoadTargetMethod;

        static ProjectionTargetProcessor()
        {
            _loadTargetMethodDefinition = typeof(ProjectionTargetProcessor)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(p => p.Name == nameof(ProjectionTargetProcessor.LoadTargetAsync) &&
                             p.IsGenericMethodDefinition);
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
            return await database.GetOneAsync(predicate, cancellation).ConfigureAwait(false);
        }

        #endregion

        private static readonly ConditionalWeakTable<Type, ITypedTargetProcessor> _typedProcessors
            = new ConditionalWeakTable<Type, ITypedTargetProcessor>();

        private static readonly ConditionalWeakTable<Type, ITypedTargetProcessor>.CreateValueCallback _buildTypedProcessor
            = BuildTypedProcessor;

        private static ITypedTargetProcessor GetTypedTargetProcessor(Type targetType)
        {
            return _typedProcessors.GetValue(targetType, _buildTypedProcessor);
        }

        private static ITypedTargetProcessor BuildTypedProcessor(Type targetType)
        {
            var result = Activator.CreateInstance(typeof(TypedTargetProcessor<>).MakeGenericType(targetType))
                as ITypedTargetProcessor;

            Debug.Assert(result != null);

            return result;
        }

        private interface ITypedTargetProcessor
        {
            ValueTask StoreAsync(IDatabaseScope databaseScope, object target, CancellationToken cancellation);
            ValueTask RemoveAsync(IDatabaseScope databaseScope, object target, CancellationToken cancellation);
        }

        private sealed class TypedTargetProcessor<TTarget> : ITypedTargetProcessor
            where TTarget : class
        {
            public ValueTask StoreAsync(IDatabaseScope databaseScope, object target, CancellationToken cancellation)
            {
                return databaseScope.StoreAsync((TTarget)target, cancellation);
            }

            public ValueTask RemoveAsync(IDatabaseScope databaseScope, object target, CancellationToken cancellation)
            {
                return databaseScope.RemoveAsync((TTarget)target, cancellation);
            }
        }
    }

    /// <summary>
    /// A projection target processor factory that can be used to create instances of type <see cref="ProjectionTargetProcessor"/>.
    /// </summary>
    public sealed class ProjectionTargetProcessorFactory : IProjectionTargetProcessorFactory
    {
        /// <inheritdoc/>
        public IProjectionTargetProcessor CreateInstance(ProjectionSourceDescriptor projectedSource, IServiceProvider serviceProvider)
        {
            var database = serviceProvider.GetRequiredService<IDatabase>();

            return new ProjectionTargetProcessor(projectedSource, database);
        }
    }

    internal sealed class ProjectionTargetMetadataEntry
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

        public static ProjectionTargetDescriptor GetDescriptor(ProjectionTargetMetadataEntry entry)
        {
            return new ProjectionTargetDescriptor(TypeResolver.Default.ResolveType(entry.TargetType.AsSpan()), entry.StringifiedTargetId);
        }
    }

    internal sealed class ProjectionSourceEntry
    {
        public ProjectionSourceEntry() { }

        public ProjectionSourceEntry(ProjectionSourceDescriptor source)
        {
            Id = source.SourceId;
            Type = source.SourceType.ToString();
        }

        public string Id { get; set; }
        public string Type { get; set; }

        public ProjectionSourceDescriptor ToDescriptor()
        {
            return new ProjectionSourceDescriptor(TypeResolver.Default.ResolveType(Type.AsSpan()), Id);
        }
    }

    internal sealed class ProjectionSourceMetadataEntry
    {
        private string _id;

        public ProjectionSourceMetadataEntry()
        {
            SourceId = SourceType = string.Empty;
        }

        public ProjectionSourceMetadataEntry(string sourceId, string sourceType)
        {
            SourceId = sourceId;
            SourceType = sourceType;
        }

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
        public List<ProjectionTargetEntry> ProjectionTargets { get; private set; } = new List<ProjectionTargetEntry>();
        public List<DependencyEntry> Dependencies { get; private set; } = new List<DependencyEntry>();
        public List<DependentEntry> Dependents { get; private set; } = new List<DependentEntry>();

        public ProjectionMetadata ToSourceMetadata()
        {
            return new ProjectionMetadata(
                Dependencies.Select(p => p.ToDescriptor()),
                ProjectionTargets.Select(p => p.ToDescriptor()),
                ProjectionRevision);
        }

        public static ProjectionSourceDescriptor GetDescriptor(ProjectionSourceMetadataEntry entry)
        {
            return new ProjectionSourceDescriptor(TypeResolver.Default.ResolveType(entry.SourceType.AsSpan()), entry.SourceId);
        }

        public static bool MatchesByRevision(ProjectionSourceMetadataEntry original, ProjectionSourceMetadataEntry comparand)
        {
            if (original is null)
                return comparand is null;

            if (comparand is null)
                return false;

            return original.MetadataRevision == comparand.MetadataRevision;
        }
    }

    internal sealed class ProjectionTargetEntry
    {
        public ProjectionTargetEntry()
        {
            Id = Type = string.Empty;
        }

        public ProjectionTargetEntry(in ProjectionTargetDescriptor descriptor)
        {
            Type = descriptor.TargetType.GetUnqualifiedTypeName();
            Id = descriptor.TargetId;
        }

        public string Id { get; set; }
        public string Type { get; set; }

        public ProjectionTargetDescriptor ToDescriptor()
        {
            return new ProjectionTargetDescriptor(TypeResolver.Default.ResolveType(Type.AsSpan()), Id);
        }
    }

    internal sealed class DependencyEntry
    {
        public DependencyEntry()
        {
            Id = Type = string.Empty;
        }

        public DependencyEntry(in ProjectionSourceDescriptor dependency, long projectionRevision)
        {
            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));

            Id = dependency.SourceId;
            Type = dependency.SourceType.GetUnqualifiedTypeName();
            ProjectionRevision = projectionRevision;
        }


        public DependencyEntry(in ProjectionSourceDependency descriptor)
        {
            Id = descriptor.Dependency.SourceId;
            Type = descriptor.Dependency.SourceType.GetUnqualifiedTypeName();
            ProjectionRevision = descriptor.ProjectionRevision;
        }

        public string Id { get; set; }
        public string Type { get; set; }

        public long ProjectionRevision { get; set; }

        public ProjectionSourceDependency ToDescriptor()
        {
            return new ProjectionSourceDependency(
                new ProjectionSourceDescriptor(TypeResolver.Default.ResolveType(Type.AsSpan()), Id),
                ProjectionRevision);
        }
    }

    internal sealed class DependentEntry
    {
        public DependentEntry()
        {
            Id = Type = string.Empty;
        }

        public DependentEntry(in ProjectionSourceDescriptor descriptor)
        {
            Id = descriptor.SourceId;
            Type = descriptor.SourceType.GetUnqualifiedTypeName();
        }

        public string Id { get; set; }
        public string Type { get; set; }

        internal ProjectionSourceDescriptor ToDescriptor()
        {
            return new ProjectionSourceDescriptor(TypeResolver.Default.ResolveType(Type.AsSpan()), Id);
        }
    }
}
