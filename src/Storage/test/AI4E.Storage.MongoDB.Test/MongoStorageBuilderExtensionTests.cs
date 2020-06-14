using System;
using Microsoft.Extensions.DependencyInjection;
using Mongo2Go;
using Xunit;

namespace AI4E.Storage.MongoDB.Test
{
    public sealed class MongoStorageBuilderExtensionTests : IDisposable
    {
        private readonly MongoDbRunner _databaseRunner;

        private StorageBuilder StorageBuilder { get; }

        public MongoStorageBuilderExtensionTests()
        {
            _databaseRunner = MongoDbRunner.Start();
            StorageBuilder = BuildStorageBuilder();
        }

        public void Dispose()
        {
            _databaseRunner.Dispose();
        }

        private StorageBuilder BuildStorageBuilder()
        {
            var storageBuilder = new StorageBuilder();
            storageBuilder.Services.Configure<MongoOptions>(options =>
            {
                options.ConnectionString = _databaseRunner.ConnectionString;
            });

            return storageBuilder;
        }

        [Fact]
        public void UseMongoDBNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                MongoStorageBuilderExtension.UseMongoDB(builder: null);
            });
        }

        [Fact]
        public void UseMongoDBConfigurationNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                MongoStorageBuilderExtension.UseMongoDB(builder: null, configuration: _ => { });
            });
        }

        [Fact]
        public void UseMongoDBDatabaseNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                MongoStorageBuilderExtension.UseMongoDB(builder: null, database: "db");
            });
        }

        [Fact]
        public void UseMongoDBConfigurationNullConfigurationThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("configuration", () =>
            {
                MongoStorageBuilderExtension.UseMongoDB(StorageBuilder, configuration: null);
            });
        }

        [Fact]
        public void UseMongoDBDatabaseNullDatabaseThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("database", () =>
            {
                MongoStorageBuilderExtension.UseMongoDB(StorageBuilder, database: null);
            });
        }

        [Fact]
        public void UseMongoDBReturnsSameStorageBuilderTest()
        {
            var storageBuilder = MongoStorageBuilderExtension.UseMongoDB(StorageBuilder);

            Assert.Same(StorageBuilder, storageBuilder);
        }

        [Fact]
        public void UseMongoDBConfigurationReturnsSameStorageBuilderTest()
        {
            var storageBuilder = MongoStorageBuilderExtension.UseMongoDB(StorageBuilder, configuration: _ => { });

            Assert.Same(StorageBuilder, storageBuilder);
        }

        [Fact]
        public void UseMongoDBDatabaseReturnsSameStorageBuilderTest()
        {
            var storageBuilder = MongoStorageBuilderExtension.UseMongoDB(StorageBuilder, database: "db");

            Assert.Same(StorageBuilder, storageBuilder);
        }

        [Fact]
        public void UseMongoDBTest()
        {
            var storageBuilder = MongoStorageBuilderExtension.UseMongoDB(StorageBuilder) as StorageBuilder;
            var database = storageBuilder.Build();

            Assert.IsType<MongoDatabase>(database);
        }

        [Fact]
        public void UseMongoDBConfigurationTest()
        {
            var storageBuilder = MongoStorageBuilderExtension.UseMongoDB(StorageBuilder, configuration: options => options.Database = "test-db") as StorageBuilder;
            var database = storageBuilder.Build();

            Assert.IsType<MongoDatabase>(database);
            Assert.Equal("test-db", ((MongoDatabase)database).UnderlyingDatabase.DatabaseNamespace.DatabaseName);
        }

        [Fact]
        public void UseMongoDBDatabaseTest()
        {
            var storageBuilder = MongoStorageBuilderExtension.UseMongoDB(StorageBuilder, "test-db") as StorageBuilder;
            var database = storageBuilder.Build();

            Assert.IsType<MongoDatabase>(database);
            Assert.Equal("test-db", ((MongoDatabase)database).UnderlyingDatabase.DatabaseNamespace.DatabaseName);
        }
    }
}
