namespace AI4E.Storage.Test.TestTypes
{
    public class IdEntry
    {
        public int Id { get; set; }
    }

    public class IdEntry<TId>
    {
        public TId Id { get; set; }
    }
}
