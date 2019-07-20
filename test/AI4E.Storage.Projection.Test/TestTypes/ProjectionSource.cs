namespace AI4E.Storage.Projection.TestTypes
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
