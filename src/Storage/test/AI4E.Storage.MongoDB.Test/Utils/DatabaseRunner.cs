using Mongo2Go;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB.Test.Utils
{
    public static class DatabaseRunner
    {
        private static readonly MongoDbRunner _databaseRunner = CreateMongoDbRunner();

        private static MongoDbRunner CreateMongoDbRunner()
        {
            var databaseRunner = MongoDbRunner.Start(
                singleNodeReplSet: true,
                additionalMongodArguments: "--setParameter \"transactionLifetimeLimitSeconds=5\"");
            var databaseClient = new MongoClient(databaseRunner.ConnectionString);

            // Workaround for https://github.com/Mongo2Go/Mongo2Go/issues/89
            databaseClient.EnsureReplicationSetReady();
            return databaseRunner;
        }

        public static MongoClient CreateClient()
        {
            return new MongoClient(_databaseRunner.ConnectionString);
        }

        public static string GetConnectionString()
        {
            return _databaseRunner.ConnectionString;
        }
    }
}
