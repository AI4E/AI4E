namespace AI4E.Storage.Domain.Projection.TestTypes
{
    public sealed class ProjectionSource
    {
        public ProjectionSource()
        {

        }

        public ProjectionSource(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
