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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;

namespace AI4E.Storage.Projection
{
    public sealed class SourceMetadataCache : ISourceMetadataCache
    {
        #region Fields

        private readonly MetadataCache<ProjectionSourceDescriptor, ProjectionSourceMetadataEntry> _sourceMetadataCache;
        private readonly IDatabase _database;

        #endregion

        #region C'tor

        public SourceMetadataCache(ProjectionSourceDescriptor projectedSource, IDatabase database)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            _sourceMetadataCache = new MetadataCache<ProjectionSourceDescriptor, ProjectionSourceMetadataEntry>(
                database,
                ProjectionSourceMetadataEntry.GetDescriptor,
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

        #endregion

        #region ISourceMetadataCache

        public ProjectionSourceDescriptor ProjectedSource { get; }

        public async ValueTask<IEnumerable<ProjectionSourceDescriptor>> GetDependentsAsync(CancellationToken cancellation)
        {
            var entry = await _sourceMetadataCache.GetEntryAsync(ProjectedSource, cancellation);

            return entry == null ? Enumerable.Empty<ProjectionSourceDescriptor>() : entry.Dependents.Select(p => p.ToDescriptor()).ToImmutableList();
        }

        public async ValueTask<SourceMetadata> GetMetadataAsync(CancellationToken cancellation)
        {
            var entry = await _sourceMetadataCache.GetEntryAsync(ProjectedSource, cancellation);

            if (entry == null)
                return default;

            return entry.ToSourceMetadata();
        }

        public async ValueTask UpdateAsync(SourceMetadata metadata, CancellationToken cancellation)
        {
            var entry = await _sourceMetadataCache.GetEntryAsync(ProjectedSource, cancellation) ??
                        new ProjectionSourceMetadataEntry(
                            ProjectedSource.SourceId,
                            ProjectedSource.SourceType.GetUnqualifiedTypeName());

            // Write the new source revision to the metadata
            entry.ProjectionRevision = metadata.ProjectionRevision;

            // Write the set of applied transactions to the metadata
            entry.ProjectionTargets.Clear();
            if (metadata.Targets.Any())
            {
                entry.ProjectionTargets.AddRange(metadata.Targets.Select(p => new ProjectionTargetEntry(p)));
            }
            else if (!entry.Dependents.Any())
            {
                _sourceMetadataCache.DeleteEntry(entry);
                return;
            }

            // Write our dependencies to the metadata
            var storedDependencies = entry.Dependencies.Select(p => p.ToDescriptor().Dependency);

            foreach (var dependency in metadata.Dependencies.Select(p => p.Dependency).Except(storedDependencies))
            {
                // Add ourself as dependent to `dependency`.
                async Task AddDependentAsync()
                {
                    if (dependency == default)
                        throw new ArgumentDefaultException(nameof(dependency));

                    var entry = await _sourceMetadataCache.GetEntryAsync(dependency, cancellation) ??
                        new ProjectionSourceMetadataEntry(
                            dependency.SourceId,
                            dependency.SourceType.GetUnqualifiedTypeName());

                    entry.Dependents.Add(new DependentEntry(ProjectedSource));
                    _sourceMetadataCache.UpdateEntry(entry);
                }

                await AddDependentAsync();
            }

            foreach (var dependency in storedDependencies.Except(metadata.Dependencies.Select(p => p.Dependency)))
            {
                // Remove ourself as dependent from `dependency`.
                async Task RemoveDependentAsync()
                {
                    if (dependency == default)
                        throw new ArgumentDefaultException(nameof(dependency));

                    var entry = await _sourceMetadataCache.GetEntryAsync(dependency, cancellation) ??
                        new ProjectionSourceMetadataEntry(
                            dependency.SourceId,
                            dependency.SourceType.GetUnqualifiedTypeName());

                    var removed = entry.Dependents.Remove(new DependentEntry(ProjectedSource));

                    Debug.Assert(removed);

                    if (entry.ProjectionTargets.Any() || entry.Dependents.Any())
                    {
                        _sourceMetadataCache.UpdateEntry(entry);
                    }
                    else
                    {
                        Debug.Assert(!entry.Dependencies.Any());
                        _sourceMetadataCache.DeleteEntry(entry);
                    }
                }

                await RemoveDependentAsync();
            }

            entry.Dependencies.Clear();
            entry.Dependencies.AddRange(metadata.Dependencies.Select(p => new DependencyEntry(p)));

            _sourceMetadataCache.UpdateEntry(entry);
        }

        public async ValueTask<bool> CommitAsync(IScopedDatabase scopedDatabase, CancellationToken cancellation)
        {
            if (scopedDatabase is null)
                throw new ArgumentNullException(nameof(scopedDatabase));

            // Write touched source metadata to database
            foreach (var cacheEntry in _sourceMetadataCache.GetEntries().Where(p => p.State != MetadataCacheEntryState.Unchanged))
            {
                // Check whether there are concurrent changes on the metadata.
                var comparandMetdata = await scopedDatabase
                    .GetAsync<ProjectionSourceMetadataEntry>(p => p.Id == cacheEntry.Entry.Id)
                    .FirstOrDefaultAsync(cancellation);

                if (!ProjectionSourceMetadataEntry.MatchesByRevision(cacheEntry.OriginalEntry, comparandMetdata))
                {
                    return false;
                }

                if (cacheEntry.State == MetadataCacheEntryState.Created
                || cacheEntry.State == MetadataCacheEntryState.Updated)
                {
                    cacheEntry.Entry.MetadataRevision = (cacheEntry.OriginalEntry?.MetadataRevision ?? 0) + 1;
                    await scopedDatabase.StoreAsync(cacheEntry.Entry, cancellation);
                }
                else
                {
                    await scopedDatabase.RemoveAsync(cacheEntry.Entry, cancellation);
                }
            }

            return true;
        }

        public void Clear()
        {
            _sourceMetadataCache.Clear();
        }

        #endregion
    }

    public sealed class MetadataCache<TId, TEntry>
        where TEntry : class
    {
        private readonly IDatabase _database;
        private readonly Func<TEntry, TId> _idAccessor;
        private readonly Func<TId, Expression<Func<TEntry, bool>>> _queryBuilder;
        private readonly IDictionary<TId, CacheEntry> _cache;

        public MetadataCache(
            IDatabase database,
            Func<TEntry, TId> idAccessor,
            Func<TId, Expression<Func<TEntry, bool>>> queryBuilder)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            if (idAccessor is null)
                throw new ArgumentNullException(nameof(idAccessor));

            if (queryBuilder is null)
                throw new ArgumentNullException(nameof(queryBuilder));

            _database = database;
            _idAccessor = idAccessor;
            _queryBuilder = queryBuilder;
        }

        public IEnumerable<MetadataCacheEntry<TEntry>> GetEntries()
        {
            var resultBuilder = ImmutableList.CreateBuilder<MetadataCacheEntry<TEntry>>();

            foreach (var cacheEntry in _cache.Values)
            {
                if (!cacheEntry.Touched)
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);

                    var originalEntryCopy = cacheEntry.OriginalEntry.DeepClone();
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(originalEntryCopy, originalEntryCopy, MetadataCacheEntryState.Unchanged));
                }
                else if (cacheEntry.Entry is null)
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);

                    var originalEntryCopy = cacheEntry.OriginalEntry.DeepClone();
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(originalEntryCopy, originalEntryCopy, MetadataCacheEntryState.Deleted));
                }
                else if (cacheEntry.OriginalEntry is null)
                {
                    Debug.Assert(cacheEntry.Entry != null);
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(cacheEntry.Entry.DeepClone(), originalEntry: null, MetadataCacheEntryState.Created));
                }
                else
                {
                    Debug.Assert(cacheEntry.OriginalEntry != null);
                    Debug.Assert(cacheEntry.Entry != null);
                    resultBuilder.Add(new MetadataCacheEntry<TEntry>(cacheEntry.Entry.DeepClone(), cacheEntry.OriginalEntry.DeepClone(), MetadataCacheEntryState.Updated));
                }
            }

            return resultBuilder.ToImmutable();
        }

        public void Clear()
        {
            _cache.Clear();
        }

        public ValueTask<TEntry> GetEntryAsync(
            TId id,
            CancellationToken cancellation)
        {
            if (_cache.TryGetValue(id, out var cacheEntry))
            {
                return new ValueTask<TEntry>(cacheEntry.Entry.DeepClone());
            }

            return GetEntryCoreAsync(id, cancellation);
        }

        private async ValueTask<TEntry> GetEntryCoreAsync(
            TId id,
            CancellationToken cancellation)
        {
            var entry = await QueryEntryAsync(id, cancellation);
            var touched = false;

            if (entry != null)
            {
                var originalEntry = entry.DeepClone();
                var cacheEntry = new CacheEntry(originalEntry, entry, touched);
                _cache[id] = cacheEntry;
            }

            return entry;
        }

        private async ValueTask<TEntry> QueryEntryAsync(
            TId id, CancellationToken cancellation)
        {
            return await _database.GetOneAsync(_queryBuilder(id), cancellation);
        }

        public void UpdateEntry(TEntry entry)
        {
            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                cacheEntry = default;
            }

            _cache[id] = new CacheEntry(cacheEntry.OriginalEntry, entry.DeepClone(), touched: true);
        }

        public void DeleteEntry(TEntry entry)
        {
            Debug.Assert(entry != null);

            var id = _idAccessor(entry);

            if (!_cache.TryGetValue(id, out var cacheEntry))
            {
                return;
            }

            if (cacheEntry.OriginalEntry is null)
            {
                _cache.Remove(id);
            }

            _cache[id] = new CacheEntry(cacheEntry.OriginalEntry, null, touched: true);
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(TEntry originalEntry, TEntry entry, bool touched)
            {
                Debug.Assert(originalEntry != null || entry != null);

                OriginalEntry = originalEntry;
                Entry = entry;
                Touched = touched;
            }

            public TEntry OriginalEntry { get; }
            public TEntry Entry { get; }
            public bool Touched { get; }
        }
    }

    public readonly struct MetadataCacheEntry<TEntry>
    {
        public MetadataCacheEntry(TEntry entry, TEntry originalEntry, MetadataCacheEntryState state)
        {
            Entry = entry;
            OriginalEntry = originalEntry;
            State = state;
        }

        public TEntry Entry { get; }
        public TEntry OriginalEntry { get; }
        public MetadataCacheEntryState State { get; }
    }

    public enum MetadataCacheEntryState
    {
        Unchanged,
        Created,
        Deleted,
        Updated
    }

    public sealed class SourceMetadataCacheFactory : ISourceMetadataCacheFactory
    {
        private readonly IDatabase _database;

        public SourceMetadataCacheFactory(IDatabase database)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            _database = database;
        }

        public ISourceMetadataCache CreateInstance(ProjectionSourceDescriptor projectedSource)
        {
            return new SourceMetadataCache(projectedSource, _database);
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

        public SourceMetadata ToSourceMetadata()
        {
            return new SourceMetadata(
                Dependencies.Select(p => p.ToDescriptor()),
                ProjectionTargets.Select(p => p.ToDescriptor()),
                ProjectionRevision);
        }

        public static ProjectionSourceDescriptor GetDescriptor(ProjectionSourceMetadataEntry entry)
        {
            return new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(entry.SourceType), entry.SourceId);
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
            return new ProjectionTargetDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(Type), Id);
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
                new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(Type), Id),
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
            return new ProjectionSourceDescriptor(TypeLoadHelper.LoadTypeFromUnqualifiedName(Type), Id);
        }
    }
}
