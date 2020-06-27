using System;
using System.Collections.Generic;
using System.Threading;

namespace AI4E.Storage.Projection.TestTypes
{
    public sealed class ProjectionRegistrationTestProjection : IProjection<ProjectionSource, ProjectionTarget>
    {
        public ProjectionRegistrationTestProjection(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IAsyncEnumerable<ProjectionTarget> ProjectAsync(ProjectionSource source, CancellationToken cancellation = default)
        {
            throw null;
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        Type IProjection.SourceType => typeof(ProjectionSource);
        Type IProjection.TargetType => typeof(ProjectionTarget);

        IAsyncEnumerable<object> IProjection.ProjectAsync(object source, CancellationToken cancellation)
        {
            return ProjectAsync(source as ProjectionSource, cancellation);
        }
#endif

        public IServiceProvider ServiceProvider { get; }
    }
}
