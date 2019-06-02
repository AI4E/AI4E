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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Projection
{
    public sealed partial class ProjectionEngine
    {
        private interface ITargetScopedProjectionEngine
        {
            Task RemoveEntityFromProjectionAsync(ProjectionSourceDescriptor projectionSource, ProjectionTargetDescriptor removedProjection, CancellationToken cancellation);

            Task UpdateEntityToProjectionAsync(ProjectionSourceDescriptor projectionSource, IProjectionResult projectionResult, bool addEntityToProjections, CancellationToken cancellation);

            Task<bool> WriteToDatabaseAsync(IScopedDatabase transactionalDatabase, CancellationToken cancellation);
        }

        private sealed class TargetScopedProjectionEngine<TTargetId, TTarget> : ITargetScopedProjectionEngine
            where TTarget : class
        {
            private readonly IDatabase _database;
            private readonly IDictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry> _targetMetadataCache;
            private readonly List<TTarget> _targetsToUpdate = new List<TTarget>();
            private readonly List<TTarget> _targetsToDelete = new List<TTarget>();

            public TargetScopedProjectionEngine(IDatabase database)
            {
                _targetMetadataCache = new Dictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry>();
                _database = database;
            }

            public async Task<bool> WriteToDatabaseAsync(IScopedDatabase transactionalDatabase, CancellationToken cancellation)
            {
                // Write touched target metadata to database
                foreach (var (originalMetadata, touchedMetadata) in _targetMetadataCache.Values.Where(p => p.Touched))
                {
                    var comparandMetdata = await transactionalDatabase
                        .GetAsync<ProjectionTargetMetadata>(p => p.Id == (originalMetadata ?? touchedMetadata).Id)
                        .FirstOrDefaultAsync(cancellation);

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

                // TODO: Do we have to check whether the targets were updated concurrently?

                foreach (var targetToUpdate in _targetsToUpdate)
                {
                    await transactionalDatabase.StoreAsync(targetToUpdate, cancellation);
                }

                foreach (var targetToDelete in _targetsToDelete)
                {
                    await transactionalDatabase.RemoveAsync(targetToDelete, cancellation);
                }

                return true;
            }

            private bool MatchesByRevision(ProjectionTargetMetadata original, ProjectionTargetMetadata comparand)
            {
                if (original is null)
                    return comparand is null;

                if (comparand is null)
                    return false;

                return original.MetadataRevision == comparand.MetadataRevision;
            }

            public async Task UpdateEntityToProjectionAsync(ProjectionSourceDescriptor projectionSource,
                                                         IProjectionResult projectionResult,
                                                         bool addEntityToProjections,
                                                         CancellationToken cancellation)
            {
                if (addEntityToProjections)
                {
                    var projectionId = GetProjectionId(projectionResult);

                    var addedProjection = new ProjectionTargetDescriptor<TTargetId>(typeof(TTarget), projectionId);
                    var (originalMetadata, metadata) = await GetMetadataAsync(addedProjection, cancellation);

                    Assert(!metadata.ProjectionSources.Any(p => p.Id == projectionSource.SourceId &&
                                                                p.Type == projectionSource.SourceType.GetUnqualifiedTypeName()));

                    var storedProjectionSource = new ProjectionSource
                    {
                        Id = projectionSource.SourceId,
                        Type = projectionSource.SourceType.GetUnqualifiedTypeName()
                    };

                    metadata.ProjectionSources.Add(storedProjectionSource);

                    _targetMetadataCache[addedProjection] = new ProjectionTargetMetadataCacheEntry(originalMetadata,
                                                                                                   metadata,
                                                                                                   touched: true);
                }

                _targetsToUpdate.Add((TTarget)projectionResult.Result);
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
                                                             p.Type == projectionSource.SourceType.GetUnqualifiedTypeName());

                Assert(removed != null);

                if (!metadata.ProjectionSources.Any())
                {
                    _targetMetadataCache[removedProjection] = new ProjectionTargetMetadataCacheEntry(originalMetadata,
                                                                                                     metadata: null,
                                                                                                     touched: true);

                    var predicate = DataPropertyHelper.BuildPredicate<TTargetId, TTarget>(metadata.TargetId);
                    var projection = await _database.GetAsync(predicate, cancellation).FirstOrDefaultAsync(cancellation);

                    if (projection != null)
                    {
                        _targetsToDelete.Add(projection);
                        //await _database.RemoveAsync(projection, cancellation);
                    }
                }

                _targetMetadataCache[removedProjection] = new ProjectionTargetMetadataCacheEntry(originalMetadata,
                                                                                                 metadata,
                                                                                                 touched: true);
            }

            private static TTargetId GetProjectionId(IProjectionResult projectionResult)
            {
                TTargetId projectionId;

                if (projectionResult is IProjectionResult<TTargetId, TTarget> typedProjectionResult)
                {
                    Assert(typedProjectionResult.ResultType == typeof(TTarget));

                    projectionId = typedProjectionResult.ResultId;
                }
                else
                {
                    Assert(projectionResult != null);
                    Assert(projectionResult.ResultType == typeof(TTarget));
                    Assert(projectionResult.ResultId != null);
                    Assert(projectionResult.ResultId is TTargetId);

                    projectionId = (TTargetId)projectionResult.ResultId;
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

            private async ValueTask<ProjectionTargetMetadataCacheEntry> GetMetadataAsync(ProjectionTargetDescriptor<TTargetId> target,
                                                                                         CancellationToken cancellation)
            {
                if (!_targetMetadataCache.TryGetValue(target, out var entry))
                {
                    var entryId = ProjectionTargetMetadata
                        .GenerateId(target.TargetId.ToString(), target.TargetType.GetUnqualifiedTypeName());

                    var metadata = await _database
                        .GetAsync<ProjectionTargetMetadata>(p => p.Id == entryId, cancellation)
                        .FirstOrDefaultAsync(cancellation);

                    var originalMetadata = metadata;
                    var touched = false;

                    if (metadata == null)
                    {
                        metadata = new ProjectionTargetMetadata
                        {
                            TargetId = target.TargetId,
                            TargetType = target.TargetType.GetUnqualifiedTypeName()
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
                    var entryId = ProjectionTargetMetadata
                        .GenerateId(target.TargetId, target.TargetType.GetUnqualifiedTypeName());

                    var metadata = await _database
                        .GetAsync<ProjectionTargetMetadata>(p => p.Id == entryId, cancellation)
                        .FirstOrDefaultAsync(cancellation);

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

                public long MetadataRevision { get; set; } = 1;

                public TTargetId TargetId { get; set; }
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
                public List<ProjectionSource> ProjectionSources { get; private set; } = new List<ProjectionSource>();
            }

            private sealed class ProjectionSource
            {
                public string Id { get; set; }
                public string Type { get; set; }
            }
        }
    }
}
