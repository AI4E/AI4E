namespace AI4E.Storage.Streaming
{
    public sealed class StreamingOptions
    {
        public int SnapshotInterval { get; set; } = 60 * 60 * 1000;

        public int SnapshotRevisionThreshold { get; set; } = 20;
    }
}
