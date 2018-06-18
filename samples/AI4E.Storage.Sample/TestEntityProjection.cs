using System.Diagnostics;
using System.Threading.Tasks;
using AI4E.Storage.Domain;
using Microsoft.AspNetCore.Mvc;

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
            Debug.Assert(testEntity != null);

            return new TestEntityModel
            {
                Id = testEntity.Id,
                Value = testEntity.Value,
                ConcurrencyToken = _entityProperties.GetConcurrencyToken(testEntity)
            };
        }

        public async Task<DependentEntityModel> ProjectAsync(DependentEntity dependentEntity,
                                                             [FromServices]IEntityStorageEngine storageEngine)
        {
            Debug.Assert(dependentEntity != null);
            Debug.Assert(storageEngine != null);

            string dependencyValue = null;

            if (!string.IsNullOrEmpty(dependentEntity.Id))
            {
                var depedency = await storageEngine.GetByIdAsync(typeof(TestEntity), dependentEntity.DependencyId) as TestEntity;
                dependencyValue = depedency.Value;
            }

            return new DependentEntityModel
            {
                Id = dependentEntity.Id,
                DependencyValue = dependencyValue
            };
        }
    }
}
