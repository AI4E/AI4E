﻿using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Storage.Specification;
using AI4E.Storage.Specification.TestTypes;
using AI4E.Utils;
using Mongo2Go;
using MongoDB.Driver;
using Xunit;

namespace AI4E.Storage.MongoDB.Test
{
    public class MongoDatabaseScopeTest : DatabaseScopeSpecification, IDisposable
    {
        private readonly MongoDbRunner _databaseRunner;
        private readonly MongoClient _databaseClient;

        public MongoDatabaseScopeTest()
        {
            _databaseRunner = MongoDbRunner.Start(singleNodeReplSet: true, additionalMongodArguments: "--setParameter \"transactionLifetimeLimitSeconds=5\"");
            _databaseClient = new MongoClient(_databaseRunner.ConnectionString);
        }

        public void Dispose()
        {
            _databaseRunner.Dispose();
        }

        private async Task AssertCollectionsExistsAsync(MongoDatabase database)
        {
            _ = await database.GetOneAsync<MinimalEntry>();
            _ = await database.GetOneAsync<ValueSumEntry>();
            _ = await database.GetOneAsync<TestEntry>();
        }

        private MongoDatabase BuildMongoDatabase()
        {
            var wrappedDatabase = _databaseClient.GetDatabase(SGuid.NewGuid().ToString());

            // Workaround for https://github.com/Mongo2Go/Mongo2Go/issues/89
            for (var i = 0; i < 10; i++)
            {
                _ = wrappedDatabase.GetCollection<DummyEntry>("__dummy");
                wrappedDatabase.DropCollection("__dummy");
            }

            return new MongoDatabase(wrappedDatabase);
        }

        protected override async Task<IDatabase> BuildDatabaseAsync()
        {
            var database = BuildMongoDatabase();
            await AssertCollectionsExistsAsync(database);
            return database;
        }

        private sealed class DummyEntry
        {
            public int Id { get; set; }
        }

        [Fact]
        public async Task GetOneAsyncNonExistingCollectionDoesNotThrow()
        {
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            _ = await scope.GetOneAsync<MinimalEntry>();
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task GetAsyncNonExistingCollectionDoesNotThrow()
        {
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            _ = await scope.GetAsync<MinimalEntry>().ToListAsync();
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task QueryAsyncNonExistingCollectionDoesNotThrow()
        {
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            _ = await scope.QueryAsync<MinimalEntry, MinimalEntry>(_ => _, cancellation: default).ToListAsync();
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task StoreAsyncNonExistingCollectionDoesNotThrow()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            await scope.StoreAsync(entry, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task RemoveAsyncNonExistingCollectionDoesNotThrow()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            await scope.RemoveAsync(entry, cancellation: default);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.False(success);
        }

        [Fact]
        public async Task GetOneAsyncNonExistingCollectionIsCreated()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            _ = await scope.GetOneAsync<MinimalEntry>();
            await scope.RollbackAsync(cancellation: default);

            await scope.StoreAsync(entry);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
        }

        [Fact]
        public async Task GetAsyncNonExistingCollectionIsCreated()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            _ = await scope.GetAsync<MinimalEntry>().ToListAsync();
            await scope.RollbackAsync(cancellation: default);

            await scope.StoreAsync(entry);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
        }

        [Fact]
        public async Task QueryAsyncNonExistingCollectionIsCreated()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            _ = await scope.QueryAsync<MinimalEntry, MinimalEntry>(_ => _, cancellation: default).ToListAsync();
            await scope.RollbackAsync(cancellation: default);

            await scope.StoreAsync(entry);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
        }

        [Fact]
        public async Task StoreAsyncNonExistingCollectionIsCreated()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            await scope.StoreAsync(entry, cancellation: default);
            await scope.RollbackAsync(cancellation: default);

            await scope.StoreAsync(entry);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
        }

        [Fact]
        public async Task RemoveAsyncNonExistingCollectionIsCreated()
        {
            var entry = new MinimalEntry { Id = 1 };
            var database = BuildMongoDatabase();
            using var scope = database.CreateScope();
            await scope.RemoveAsync(entry, cancellation: default);
            await scope.RollbackAsync(cancellation: default);

            await scope.StoreAsync(entry);
            var success = await scope.TryCommitAsync(cancellation: default);

            Assert.True(success);
        }
    }
}