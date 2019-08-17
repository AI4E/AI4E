using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Projection.Mocks
{
    public sealed class ProjectionSourceProcessorMock : IProjectionSourceProcessor
    {
        public ProjectionSourceProcessorMock(ProjectionSourceDescriptor projectedSource)
        {
            ProjectedSource = projectedSource;
            Sources = new Dictionary<ProjectionSourceDescriptor, (object source, long sourceRevision)>();
        }

        public Dictionary<ProjectionSourceDescriptor, (object source, long sourceRevision)> Sources { get; }

        public ProjectionSourceDescriptor ProjectedSource { get; }

        public ValueTask<object> GetSourceAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation = default)
        {
            if (!Sources.TryGetValue(projectionSource, out var entry))
            {
                return new ValueTask<object>(result: null);
            }

            return new ValueTask<object>(entry.source);
        }

        public ValueTask<long> GetSourceRevisionAsync(
            ProjectionSourceDescriptor projectionSource,
            bool bypassCache,
            CancellationToken cancellation = default)
        {
            if (!Sources.TryGetValue(projectionSource, out var entry))
            {
                return new ValueTask<long>(result: 0);
            }

            return new ValueTask<long>(entry.sourceRevision);
        }

        public IEnumerable<ProjectionSourceDependency> Dependencies =>
            from entry in Sources
            let descriptor = entry.Key
            let revision = entry.Value.sourceRevision
            where descriptor != ProjectedSource
            select new ProjectionSourceDependency(descriptor, revision);

        public sealed class Factory : IProjectionSourceProcessorFactory
        {
            public Factory Instance { get; } = new Factory();

            private Factory() { }

            public IProjectionSourceProcessor CreateInstance(ProjectionSourceDescriptor projectedSource, IServiceProvider serviceProvider)
            {
                return new ProjectionSourceProcessorMock(projectedSource);
            }
        }
    }
}
