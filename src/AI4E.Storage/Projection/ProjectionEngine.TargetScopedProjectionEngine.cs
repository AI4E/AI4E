using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Transactions;
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

            Task<bool> WriteToDatabaseAsync(IScopedTransactionalDatabase transactionalDatabase, CancellationToken cancellation);
        }

        private sealed class TargetScopedProjectionEngine<TProjectionId, TProjection> : ITargetScopedProjectionEngine
            where TProjection : class
        {
            private readonly IDatabase _database;
            private readonly IDictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry> _targetMetadataCache;
            private readonly List<TProjection> _targetsToUpdate = new List<TProjection>();
            private readonly List<TProjection> _targetsToDelete = new List<TProjection>();

            public TargetScopedProjectionEngine(IDatabase database)
            {
                _targetMetadataCache = new Dictionary<ProjectionTargetDescriptor, ProjectionTargetMetadataCacheEntry>();
                _database = database;
            }

            public async Task<bool> WriteToDatabaseAsync(IScopedTransactionalDatabase transactionalDatabase, CancellationToken cancellation)
            {
                // Write touched target metadata to database
                foreach (var (originalMetadata, touchedMetadata) in _targetMetadataCache.Values.Where(p => p.Touched))
                {
                    var comparandMetdata = await transactionalDatabase.GetAsync<ProjectionTargetMetadata>(p => p.Id == (originalMetadata ?? touchedMetadata).Id).FirstOrDefault();

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

                _targetsToUpdate.Add((TProjection)projectionResult.Result);
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
                        _targetsToDelete.Add(projection);
                        //await _database.RemoveAsync(projection, cancellation);
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

                public long MetadataRevision { get; set; } = 1;

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
