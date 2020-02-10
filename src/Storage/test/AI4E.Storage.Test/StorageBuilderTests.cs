using System;
using System.Linq;
using AI4E.Storage.Test.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class StorageBuilderTests
    {
        private StorageBuilder StorageBuilder { get; } = new StorageBuilder();

        [Fact]
        public void CtorTest()
        {
            Assert.NotNull(StorageBuilder.Services);
        }

        [Fact]
        public void BuildTest()
        {
            StorageBuilder.Services.AddSingleton<IDatabase, DatabaseMock>();
            var database = StorageBuilder.Build();

            Assert.NotNull(database);
            Assert.IsType<DatabaseMock>(database);
        }
    }

    public sealed class StorageBuilderExtensionTests
    {
        private StorageBuilder StorageBuilder { get; } = new StorageBuilder();

        [Fact]
        public void UseDatabaseNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                StorageBuilderExtension.UseDatabase<DatabaseMock>(builder: null);
            });
        }

        [Fact]
        public void UseDatabaseFactoryNullBuilderThrowsNullReferenceExceptionTest()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                StorageBuilderExtension.UseDatabase(builder: null, provider => new DatabaseMock(provider));
            });
        }

        [Fact]
        public void UseDatabaseFactoryNullFactoryThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("factory", () =>
            {
                StorageBuilderExtension.UseDatabase<DatabaseMock>(StorageBuilder, factory: null);
            });
        }

        [Fact]
        public void UseDatabaseRegistersDatabaseTest()
        {
            StorageBuilderExtension.UseDatabase<DatabaseMock>(StorageBuilder);

            var serviceDescriptor = StorageBuilder.Services.LastOrDefault(p => p.ServiceType == typeof(IDatabase));

            Assert.NotNull(serviceDescriptor);
            Assert.Equal(typeof(DatabaseMock), serviceDescriptor.ImplementationType);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
        }

        [Fact]
        public void UseDatabaseFactoryRegistersDatabaseTest()
        {
            Func<IServiceProvider, DatabaseMock> factory = provider => new DatabaseMock(provider);
            StorageBuilderExtension.UseDatabase(StorageBuilder, factory);

            var serviceDescriptor = StorageBuilder.Services.LastOrDefault(p => p.ServiceType == typeof(IDatabase));

            Assert.NotNull(serviceDescriptor);
            Assert.Same(factory, serviceDescriptor.ImplementationFactory);
            Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
        }
    }
}
