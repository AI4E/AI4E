using System;
using AI4E.Storage.Specification;
using AI4E.Utils;
using Mongo2Go;
using MongoDB.Driver;

namespace AI4E.Storage.MongoDB.Test
{
    public class MongoDatabaseTest : DatabaseSpecification, IDisposable
    {
        private readonly MongoDbRunner _databaseRunner;
        private readonly MongoClient _databaseClient;

        public MongoDatabaseTest()
        {
            _databaseRunner = MongoDbRunner.Start();
            _databaseClient = new MongoClient(_databaseRunner.ConnectionString);
        }

        public void Dispose()
        {
            _databaseRunner.Dispose();
        }

        protected override IDatabase BuildDatabase()
        {
            var wrappedDatabase = _databaseClient.GetDatabase(SGuid.NewGuid().ToString());
            return new MongoDatabase(wrappedDatabase);
        }
    }
}
