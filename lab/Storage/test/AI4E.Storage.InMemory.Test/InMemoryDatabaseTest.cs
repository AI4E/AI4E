using AI4E.Storage.Specification;

namespace AI4E.Storage.InMemory.Test
{
    public sealed class InMemoryDatabaseTest : DatabaseSpecification
    {
        protected override IDatabase BuildDatabase()
        {
            return new InMemoryDatabase();
        }
    }
}
