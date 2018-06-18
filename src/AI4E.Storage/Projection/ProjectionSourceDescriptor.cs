using System;

namespace AI4E.Storage.Projection
{
    public readonly struct ProjectionSourceDescriptor : IEquatable<ProjectionSourceDescriptor>
    {
        public ProjectionSourceDescriptor(Type sourceType, string sourceId)
        {
            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));

            if (sourceId.Equals(default))
                throw new ArgumentDefaultException(nameof(sourceId));

            SourceType = sourceType;
            SourceId = sourceId;
        }

        public Type SourceType { get; }
        public string SourceId { get; }

        public override bool Equals(object obj)
        {
            return obj is ProjectionSourceDescriptor entityDescriptor && Equals(entityDescriptor);
        }

        public bool Equals(ProjectionSourceDescriptor other)
        {
            return other.SourceType == null && SourceType == null || other.SourceType == SourceType && other.SourceId.Equals(SourceId);
        }

        public override int GetHashCode()
        {
            if (SourceType == null)
                return 0;

            return SourceType.GetHashCode() ^ SourceId.GetHashCode();
        }

        public static bool operator ==(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ProjectionSourceDescriptor left, in ProjectionSourceDescriptor right)
        {
            return !left.Equals(right);
        }
    }
}
