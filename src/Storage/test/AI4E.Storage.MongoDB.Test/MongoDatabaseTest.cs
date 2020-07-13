using AI4E.Storage.MongoDB.Test.Utils;
using AI4E.Storage.Specification;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB.Test
{
    public class MongoDatabaseTest : DatabaseSpecification
    {
        private readonly MongoClient _databaseClient = DatabaseRunner.CreateClient();

        protected override IDatabase BuildDatabase()
        {
            var wrappedDatabase = _databaseClient.GetDatabase(DatabaseName.GenerateRandom());
            return new MongoDatabase(wrappedDatabase);
        }
    }
}
