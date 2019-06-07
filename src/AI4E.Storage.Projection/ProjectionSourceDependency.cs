using System;

namespace AI4E.Storage.Projection
{
    public readonly struct ProjectionSourceDependency : IEquatable<ProjectionSourceDependency>
    {
        public ProjectionSourceDependency(Type sourceType, string sourceId, long projectionRevision)
            : this(new ProjectionSourceDescriptor(sourceType, sourceId), projectionRevision)
        { }

        public ProjectionSourceDependency(in ProjectionSourceDescriptor dependency, long projectionRevision)
        {
            if (projectionRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(projectionRevision));

            Dependency = dependency;
            ProjectionRevision = projectionRevision;
        }

        public ProjectionSourceDescriptor Dependency { get; }
        public long ProjectionRevision { get; }

        public bool Equals(ProjectionSourceDependency other)
        {
            return (Dependency, ProjectionRevision) == (other.Dependency, other.ProjectionRevision);
        }

        public override bool Equals(object obj)
        {
            return obj is ProjectionSourceDependency projectionSourceDependency
                && Equals(projectionSourceDependency);
        }

        public override int GetHashCode()
        {
            return (Dependency, ProjectionRevision).GetHashCode();
        }

        public static bool operator ==(in ProjectionSourceDependency left, in ProjectionSourceDependency right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ProjectionSourceDependency left, in ProjectionSourceDependency right)
        {
            return !left.Equals(right);
        }

        public static implicit operator ProjectionSourceDependency(in (ProjectionSourceDescriptor dependency, long projectionRevision) x)
        {
            return new ProjectionSourceDependency(x.dependency, x.projectionRevision);
        }

        public void Deconstruct(out ProjectionSourceDescriptor dependency, out long projectionRevision)
        {
            dependency = Dependency;
            projectionRevision = ProjectionRevision;
        }
    }
}
