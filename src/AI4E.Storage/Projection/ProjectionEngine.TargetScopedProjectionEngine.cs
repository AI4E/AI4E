using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Transactions;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Projection
{
    public sealed partial class ProjectionEngine
    {
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
                    var entryId = ProjectionTargetMetadata.GenerateId(target.TargetId.ToString(), target.TargetType.GetUnqualifiedTypeName());
                    var metadata = await _database.GetAsync<ProjectionTargetMetadata>(p => p.Id == entryId, cancellation).FirstOrDefault();
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
                    var entryId = ProjectionTargetMetadata.GenerateId(target.TargetId, target.TargetType.GetUnqualifiedTypeName());
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
