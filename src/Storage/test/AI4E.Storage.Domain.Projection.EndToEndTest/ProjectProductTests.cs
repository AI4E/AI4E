using System;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Storage.Domain.EndToEndTestAssembly;
using AI4E.Storage.Domain.EndToEndTestAssembly.API;
using AI4E.Storage.Domain.EndToEndTestAssembly.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.Storage.Domain.Projection.EndToEndTest
{
    public class ProjectProductTests
    {
        private IServiceProvider ServiceProvider { get; }
        private IMessageDispatcher MessageDispatcher { get; }

        public ProjectProductTests()
        {
            var serviceCollection = new ServiceCollection();
            var setup = new Setup(activateProjections: true);
            setup.ConfigureServices(serviceCollection);

            ServiceProvider = serviceCollection.BuildServiceProvider(validateScopes: true);
            MessageDispatcher = ServiceProvider.GetRequiredService<IMessagingEngine>().CreateDispatcher();
        }

        [Fact]
        public async Task QueryModelAfterCreatingProductTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(command);
            var query = new ByIdQuery<ProjectedProductModel>(command.Id);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResult<ProjectedProductModel>(out var productModel));
            Assert.Equal(command.Id, productModel.Id);
            Assert.NotNull(productModel.ConcurrencyToken);
            Assert.NotEmpty(productModel.ConcurrencyToken);
            Assert.Equal(command.Name, productModel.Name);
            Assert.Equal(command.Price, productModel.Price);
        }

        [Fact]
        public async Task QueryNonExistingModelIsNotFoundTest()
        {
            // Arrange
            var query = new ByIdQuery<ProjectedProductModel>(Guid.NewGuid());

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsNotFound());
        }

        [Fact]
        public async Task QueryListModelAfterCreatingProductTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(command);
            var query = new Query<ProjectedProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProjectedProductListModel>(out var listModels));
            Assert.Collection(listModels, listModel =>
            {
                Assert.Equal(command.Id, listModel.Id);
                Assert.Equal(command.Name, listModel.Name);
                Assert.Equal(command.Price, listModel.Price);
            });
        }

        [Fact]
        public async Task QueryNonExistingListModelIsEmptyCollectionTest()
        {
            // Arrange
            var query = new Query<ProjectedProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProjectedProductListModel>(out var listModels));
            Assert.Empty(listModels);
        }

        [Fact]
        public async Task QueryModelAfterUpdatingProductTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var updateCommand = new ProductUpdateCommand(createCommand.Id, concurrencyToken, "def", 234);
            await MessageDispatcher.DispatchAsync(updateCommand);
            var query = new ByIdQuery<ProjectedProductModel>(updateCommand.Id);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResult<ProjectedProductModel>(out var productModel));
            Assert.Equal(updateCommand.Id, productModel.Id);
            Assert.NotNull(productModel.ConcurrencyToken);
            Assert.NotEmpty(productModel.ConcurrencyToken);
            Assert.Equal(updateCommand.Name, productModel.Name);
            Assert.Equal(updateCommand.Price, productModel.Price);
        }

        [Fact]
        public async Task QueryListModelAfterUpdatingProductTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var updateCommand = new ProductUpdateCommand(createCommand.Id, concurrencyToken, "def", 234);
            await MessageDispatcher.DispatchAsync(updateCommand);
            var query = new Query<ProjectedProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProjectedProductListModel>(out var listModels));
            Assert.Collection(listModels, listModel =>
            {
                Assert.Equal(updateCommand.Id, listModel.Id);
                Assert.Equal(updateCommand.Name, listModel.Name);
                Assert.Equal(updateCommand.Price, listModel.Price);
            });
        }

        [Fact]
        public async Task QueryModelAfterDeletingProductTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var deleteCommand = new ProductDeleteCommand(createCommand.Id, concurrencyToken);
            await MessageDispatcher.DispatchAsync(deleteCommand);
            var query = new ByIdQuery<ProjectedProductModel>(deleteCommand.Id);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsNotFound());
        }

        [Fact]
        public async Task QueryListModelAfterDeletingProductTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var deleteCommand = new ProductDeleteCommand(createCommand.Id, concurrencyToken);
            await MessageDispatcher.DispatchAsync(deleteCommand);
            var query = new Query<ProjectedProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProjectedProductListModel>(out var listModels));
            Assert.Empty(listModels);
        }
    }
}
