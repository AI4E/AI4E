using System;
using AI4E.Storage.Test.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class ServiceCollectionExtensionTests
    {
        private IServiceProvider ConfigureServices(Action<IServiceCollection> configuration)
        {
            var services = new ServiceCollection();
            configuration(services);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void AddStorageNullServicesThrowsNullReferenceException()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                ServiceCollectionExtension.AddStorage(services: null);
            });
        }

        [Fact]
        public void AddStorageAddsNullDatabaseTest()
        {
            var serviceProvider = ConfigureServices(services => services.AddStorage());
            var database = serviceProvider.GetRequiredService<IDatabase>();

            Assert.IsType<NoDatabase>(database);
        }

        [Fact]
        public void AddStorageDoesNotOverrideRegisteredDatabaseTest()
        {
            var serviceProvider = ConfigureServices(services =>
            {
                services.AddSingleton<IDatabase, DatabaseMock>();
                services.AddStorage();
            });

            var database = serviceProvider.GetRequiredService<IDatabase>();

            Assert.IsType<DatabaseMock>(database);
        }

        [Fact]
        public void AddStorageReturnsValidStorageBuilderTest()
        {
            var services = new ServiceCollection();
            var storageBuilder = services.AddStorage();

            Assert.NotNull(storageBuilder);
            Assert.Same(services, storageBuilder.Services);
        }

        [Fact]
        public void AddStorageSubsequentCallReturnsSameStorageBuilderTest()
        {
            var services = new ServiceCollection();
            var expectedStorageBuilder = services.AddStorage();
            var testStorageBuilder = services.AddStorage();

            Assert.Same(expectedStorageBuilder, testStorageBuilder);
        }
    }
}
