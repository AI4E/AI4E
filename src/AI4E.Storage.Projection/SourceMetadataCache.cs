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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;

namespace AI4E.Storage.Projection
{
    internal sealed class SourceMetadataCache
    {
        private readonly IDictionary<ProjectionSourceDescriptor, CacheEntry> _sourceMetadataCache;
        private readonly IDatabase _database;

        public SourceMetadataCache(IDatabase database)
        {
            if (database is null)
                throw new ArgumentNullException(nameof(database));

            _sourceMetadataCache = new Dictionary<ProjectionSourceDescriptor, CacheEntry>();
            _database = database;
        }

        public ValueTask<IProjectionSourceMetdata> GetMetadataAsync(
            ProjectionSourceDescriptor sourceDescriptor,
            bool createIfNonExistent,
            CancellationToken cancellation)
        {
            if (!_sourceMetadataCache.TryGetValue(sourceDescriptor, out var entry))
            {
                return GetMetadataCoreAsync(sourceDescriptor, createIfNonExistent, cancellation);
            }

            if (entry.Metadata == null && createIfNonExistent)
            {
                var metadata = new ProjectionSourceMetadataEntry
                {
                    SourceId = sourceDescriptor.SourceId,
                    SourceType = sourceDescriptor.SourceType.GetUnqualifiedTypeName()
                };

                entry = new CacheEntry(entry.OriginalMetadata, metadata, touched: true);
                _sourceMetadataCache[sourceDescriptor] = entry;
            }

            return new ValueTask<IProjectionSourceMetdata>(new ProjectionSourceMetdata(entry));
        }

        public async ValueTask<IProjectionSourceMetdata> GetMetadataCoreAsync(
            ProjectionSourceDescriptor sourceDescriptor,
            bool createIfNonExistent,
            CancellationToken cancellation)
        {
            var metadata = await QueryMetadataAsync(sourceDescriptor, cancellation);
            var touched = false;

            if (metadata == null && createIfNonExistent)
            {
                metadata = new ProjectionSourceMetadataEntry
                {
                    SourceId = sourceDescriptor.SourceId,
                    SourceType = sourceDescriptor.SourceType.GetUnqualifiedTypeName()
                };

                touched = true;
            }

            var originalMetadata = default(ProjectionSourceMetadataEntry);

            if (metadata != null)
            {
                originalMetadata = metadata.DeepClone();
            }

            var entry = new CacheEntry(originalMetadata, metadata, touched);
            _sourceMetadataCache[sourceDescriptor] = entry;

            return new ProjectionSourceMetdata(entry);
        }

        private async ValueTask<ProjectionSourceMetadataEntry> QueryMetadataAsync(
            ProjectionSourceDescriptor sourceDescriptor, CancellationToken cancellation)
        {
            var entryId = ProjectionSourceMetadataEntry.GenerateId(
                sourceDescriptor.SourceId, sourceDescriptor.SourceType.GetUnqualifiedTypeName());

            return await _database
                .GetAsync<ProjectionSourceMetadataEntry>(p => p.Id == entryId, cancellation)
                .FirstOrDefaultAsync(cancellation);
        }

        public void Update(IProjectionSourceMetdata metdata)
        {
            if (metdata is null)
                throw new ArgumentNullException(nameof(metdata));

            var sourceType = metdata.SourceType;
            var sourceId = metdata.SourceId;
            var sourceDescriptor = new ProjectionSourceDescriptor(sourceType, sourceId);

            (metdata as ProjectionSourceMetdata).Touched = true;
            _sourceMetadataCache[sourceDescriptor] = (metdata as ProjectionSourceMetdata).ToCacheEntry();
        }

        public void Delete(IProjectionSourceMetdata metdata)
        {
            if (metdata is null)
                throw new ArgumentNullException(nameof(metdata));

            var sourceType = metdata.SourceType;
            var sourceId = metdata.SourceId;
            var sourceDescriptor = new ProjectionSourceDescriptor(sourceType, sourceId);

            var original = (metdata as ProjectionSourceMetdata).Original;

            if (original is null)
            {
                _sourceMetadataCache.Remove(sourceDescriptor);
            }
            else
            {
                _sourceMetadataCache[sourceDescriptor] = new CacheEntry(original, null, touched: true);
            }
        }

        public void Clear()
        {
            _sourceMetadataCache.Clear();
        }

        public async Task<bool> CommitAsync(IScopedDatabase scopedDatabase, CancellationToken cancellation)
        {
            if (scopedDatabase is null)
                throw new ArgumentNullException(nameof(scopedDatabase));

            // Write touched source metadata to database
            foreach (var (originalMetadata, touchedMetadata) in _sourceMetadataCache.Values.Where(p => p.Touched))
            {
                var comparandMetdata = await scopedDatabase
                    .GetAsync<ProjectionSourceMetadataEntry>(p => p.Id == (originalMetadata ?? touchedMetadata).Id)
                    .FirstOrDefaultAsync(cancellation);

                if (!MatchesByRevision(originalMetadata, comparandMetdata))
                {
                    return false;
                }

                if (touchedMetadata == null)
                {
                    Debug.Assert(originalMetadata != null);

                    await scopedDatabase.RemoveAsync(originalMetadata, cancellation);
                }
                else
                {
                    touchedMetadata.MetadataRevision = originalMetadata?.MetadataRevision ?? 1;
                    await scopedDatabase.StoreAsync(touchedMetadata, cancellation);
                }
            }

            return true;
        }

        private bool MatchesByRevision(ProjectionSourceMetadataEntry original, ProjectionSourceMetadataEntry comparand)
        {
            if (original is null)
                return comparand is null;

            if (comparand is null)
                return false;

            return original.MetadataRevision == comparand.MetadataRevision;
        }

        private sealed class ProjectionSourceMetdata : IProjectionSourceMetdata
        {
            public ProjectionSourceMetdata(in CacheEntry cacheEntry)
                : this(cacheEntry.Metadata, cacheEntry.OriginalMetadata, cacheEntry.Touched)
            { }

            public ProjectionSourceMetdata(
                ProjectionSourceMetadataEntry entry,
                ProjectionSourceMetadataEntry original,
                bool touched)
            {
                Debug.Assert(entry != null);

                Entry = entry;
                Original = original;
                Touched = touched;

                ProjectionTargets = new WrapperCollection<ProjectionTargetDescriptor, ProjectionTargetEntry>(
                    entry.ProjectionTargets,
                    unwrap: descriptor => new ProjectionTargetEntry(descriptor),
                    wrap: entry => entry.ToDescriptor());

                Dependencies = new WrapperCollection<ProjectionSourceDependency, DependencyEntry>(
                    entry.Dependencies,
                    unwrap: descriptor => new DependencyEntry(descriptor),
                    wrap: entry => entry.ToDescriptor());

                Dependents = new WrapperCollection<ProjectionSourceDescriptor, DependentEntry>(
                    entry.Dependents,
                    unwrap: descriptor => new DependentEntry(descriptor),
                    wrap: entry => entry.ToDescriptor());
            }

            public ProjectionSourceMetadataEntry Entry { get; }
            public ProjectionSourceMetadataEntry Original { get; }
            public bool Touched { get; set; }

            public string SourceId => Entry.SourceId;

            public Type SourceType => TypeLoadHelper.LoadTypeFromUnqualifiedName(Entry.SourceType);

            public long ProjectionRevision
            {
                get => Entry.ProjectionRevision;
                set
                {
                    if (value < 0)
                        throw new ArgumentOutOfRangeException(nameof(value));

                    Entry.ProjectionRevision = value;
                }
            }

            public ICollection<ProjectionTargetDescriptor> ProjectionTargets { get; }

            public ICollection<ProjectionSourceDependency> Dependencies { get; }

            public ICollection<ProjectionSourceDescriptor> Dependents { get; }

            public CacheEntry ToCacheEntry()
            {
                return new CacheEntry(Original, Entry, Touched);
            }
        }

        private readonly struct CacheEntry
        {
            public CacheEntry(ProjectionSourceMetadataEntry originalMetadata, ProjectionSourceMetadataEntry metadata, bool touched)
            {
                if (originalMetadata == null && metadata == null)
                    touched = false;

                OriginalMetadata = originalMetadata;
                Metadata = metadata;
                Touched = touched;
            }

            public ProjectionSourceMetadataEntry OriginalMetadata { get; }
            public ProjectionSourceMetadataEntry Metadata { get; }
            public bool Touched { get; }

            public void Deconstruct(out ProjectionSourceMetadataEntry originalMetadata,
                                    out ProjectionSourceMetadataEntry metadata)
            {
                originalMetadata = OriginalMetadata;
                metadata = Metadata;
            }
        }

        private sealed class ProjectionSourceMetadataEntry
        {
            private string _id;

            public ProjectionSourceMetadataEntry()
            {
                SourceId = SourceType = string.Empty;
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
        }

        private sealed class ProjectionTargetEntry
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

        private sealed class DependencyEntry
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

        private sealed class DependentEntry
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

    public interface IProjectionSourceMetdata
    {
        ICollection<ProjectionSourceDependency> Dependencies { get; }
        ICollection<ProjectionSourceDescriptor> Dependents { get; }
        long ProjectionRevision { get; set; }
        ICollection<ProjectionTargetDescriptor> ProjectionTargets { get; }
        string SourceId { get; }
        Type SourceType { get; }
    }
}
