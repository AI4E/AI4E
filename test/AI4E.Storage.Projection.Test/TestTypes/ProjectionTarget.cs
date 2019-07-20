namespace AI4E.Storage.Projection.TestTypes
{
    public sealed class ProjectionTarget
    {
        public ProjectionTarget()
        {

        }

        public ProjectionTarget(ProjectionSource source)
        {
            Source = source;
        }

        public ProjectionSource Source { get; }
    }
}
