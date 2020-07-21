using System;
using System.Threading.Tasks;
using AI4E.Storage.MongoDB.Test.Utils;
using AutoFixture;
using AutoFixture.AutoMoq;
using MongoDB.Driver;
using Xunit;

namespace AI4E.Storage.MongoDB.Test
{
    public sealed class SerializeRuntimeTypeTests
    {
        private readonly MongoClient _databaseClient = DatabaseRunner.CreateClient();

        private IDatabase BuildDatabase()
        {
            var wrappedDatabase = _databaseClient.GetDatabase(DatabaseName.GenerateRandom());
            return new MongoDatabase(wrappedDatabase);
        }

        private IFixture Fixture { get; }

        public SerializeRuntimeTypeTests()
        {
            Fixture = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true });
            Fixture.Register(BuildDatabase);
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(Action))]
        // [InlineData(typeof(Func<>))]
        [InlineData(typeof(Enum))]
        [InlineData(typeof(ValueType))]
        [InlineData(typeof(StoredInstance))]
        [InlineData(typeof(void))]
        [InlineData(null)]
        public async Task CorrectlySerializedRuntimeTypeTest(Type? runtimeType)
        {
            // Arrange
            var storedInstance = new StoredInstance { RuntimeType = runtimeType };
            var subject = Fixture.Create<IDatabase>();

            // Act
            var storeSuccess = await subject.AddAsync(storedInstance);
            var readStoredInstance = await subject.GetOneAsync<StoredInstance>();

            // Assert
            Assert.True(storeSuccess);
            Assert.Equal(runtimeType, readStoredInstance.RuntimeType);
        }

        private sealed class StoredInstance
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public Type? RuntimeType { get; set; }
        }
    }
}
