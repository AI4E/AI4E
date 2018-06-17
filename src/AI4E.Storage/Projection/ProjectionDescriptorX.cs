using System;

namespace AI4E.Storage.Projection
{
    public readonly struct ProjectionDescriptorX : IEquatable<ProjectionDescriptorX> // TODO
    {
        public ProjectionDescriptorX(Type projectionType, string projectionId)
        {
            if (projectionType == null)
                throw new ArgumentNullException(nameof(projectionType));

            if (projectionId.Equals(default))
                throw new ArgumentDefaultException(nameof(projectionId));

            ProjectionType = projectionType;
            ProjectionId = projectionId;
        }

        public Type ProjectionType { get; }
        public string ProjectionId { get; }

        public override bool Equals(object obj)
        {
            return obj is ProjectionDescriptorX entityDescriptor && Equals(entityDescriptor);
        }

        public bool Equals(ProjectionDescriptorX other)
        {
            return other.ProjectionType == null && ProjectionType == null || other.ProjectionType == ProjectionType && other.ProjectionId.Equals(ProjectionId);
        }

        public override int GetHashCode()
        {
            if (ProjectionType == null)
                return 0;

            return ProjectionType.GetHashCode() ^ ProjectionId.GetHashCode();
        }

        public static bool operator ==(in ProjectionDescriptorX left, in ProjectionDescriptorX right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ProjectionDescriptorX left, in ProjectionDescriptorX right)
        {
            return !left.Equals(right);
        }
    }
}
