using AI4E.Storage.Domain;

namespace AI4E.Storage.Sample
{
    public sealed class TestEntityProjection
    {
        private readonly IEntityStoragePropertyManager _entityProperties;

        public TestEntityProjection(IEntityStoragePropertyManager entityProperties) // TODO: Rename
        {
            _entityProperties = entityProperties;
        }

        public TestEntityModel Project(TestEntity testEntity)
        {
            return new TestEntityModel
            {
                Id = testEntity.Id,
                Value = testEntity.Value,
                ConcurrencyToken = _entityProperties.GetConcurrencyToken(testEntity)
            };
        }
    }
}
