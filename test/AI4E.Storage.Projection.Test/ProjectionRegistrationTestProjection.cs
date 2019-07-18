using System;
using System.Collections.Generic;
using System.Threading;

namespace AI4E.Storage.Projection
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

        public IServiceProvider ServiceProvider { get; }
    }
}
