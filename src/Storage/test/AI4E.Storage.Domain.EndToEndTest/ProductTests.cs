using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Storage.Domain.EndToEndTest.Utils;
using AI4E.Storage.Domain.EndToEndTestAssembly;
using AI4E.Storage.Domain.EndToEndTestAssembly.API;
using AI4E.Storage.Domain.EndToEndTestAssembly.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI4E.Storage.Domain.EndToEndTest
{
    public class ProductTests
    {
        private IServiceProvider ServiceProvider { get; }
        private IMessageDispatcher MessageDispatcher { get; }

        private MessageRecorder<object> MessageRecorder { get; }

        public ProductTests()
        {
            var serviceCollection = new ServiceCollection();
            var setup = new Setup();
            setup.ConfigureServices(serviceCollection);

            serviceCollection.AddMessageRecorder<object>();

            ServiceProvider = serviceCollection.BuildServiceProvider(validateScopes: true);
            MessageDispatcher = ServiceProvider.GetRequiredService<IMessageDispatcher>();
            MessageRecorder = ServiceProvider.GetRequiredService<MessageRecorder<object>>();
        }


        [Fact]
        public async Task CreateProductIsSuccessTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(command);

            // Assert
            Assert.True(dispatchResult.IsSuccess);
        }

        [Fact]
        public async Task CreateProductReturnsConcurrencyTokenTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(command);
            var concurrencyToken = dispatchResult.ResultData["ConcurrencyToken"]?.ToString();

            // Assert
            Assert.NotNull(concurrencyToken);
            Assert.NotEmpty(concurrencyToken);
        }

        [Fact]
        public async Task CreateProductValidationTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "  ", -1);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(command);

            // Assert
            Assert.True(dispatchResult.IsValidationFailed(out var validationResults));
            Assert.Equal(2, validationResults.Count());
        }

        [Fact]
        public async Task CreateProductWithSameIdAsExistingIsFailureTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(command);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(command);

            // Assert
            Assert.True(dispatchResult.IsEntityAlreadyPresent());
        }

        [Fact]
        public async Task CreateProductDispatchesEventsTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            MessageRecorder.Clear();

            // Act
            await MessageDispatcher.DispatchAsync(command);

            // Assert
            Assert.Contains(MessageRecorder.RecordedMessages, dispatchData =>
            {
                if (dispatchData.MessageType != typeof(ProductCreated))
                    return false;

                var evt = dispatchData.Message as ProductCreated;

                Assert.Equal(command.Id, evt.Id);
                Assert.Equal(command.Name, evt.Name);
                Assert.Equal(command.Price, evt.Price);

                return true;
            });
        }

        [Fact]
        public async Task QueryModelAfterCreatingProductTest()
        {
            // Arrange
            var command = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(command);
            var query = new ByIdQuery<ProductModel>(command.Id);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResult<ProductModel>(out var productModel));
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
            var query = new ByIdQuery<ProductModel>(Guid.NewGuid());

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
            var query = new Query<ProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProductListModel>(out var listModels));
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
            var query = new Query<ProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProductListModel>(out var listModels));
            Assert.Empty(listModels);
        }

        [Fact]
        public async Task UpdateProductIsSuccessTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var updateCommand = new ProductUpdateCommand(createCommand.Id, concurrencyToken, "def", 234);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(updateCommand);

            // Assert
            Assert.True(dispatchResult.IsSuccess);
        }

        [Fact]
        public async Task UpdateProductValidationTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var updateCommand = new ProductUpdateCommand(createCommand.Id, concurrencyToken, "  ", -1);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(updateCommand);

            // Assert
            Assert.True(dispatchResult.IsValidationFailed(out var validationResults));
            Assert.Equal(2, validationResults.Count());
        }

        [Fact]
        public async Task UpdateProductWithDefaultConcurrencyTokenFailsTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(createCommand);
            var updateCommand = new ProductUpdateCommand(createCommand.Id, string.Empty, "def", 234);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(updateCommand);

            // Assert
            Assert.True(dispatchResult.IsConcurrencyIssue());
        }

        [Fact]
        public async Task UpdateProductWithWrongConcurrencyTokenFailsTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(createCommand);
            var updateCommand = new ProductUpdateCommand(createCommand.Id, Guid.NewGuid().ToString(), "def", 234);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(updateCommand);

            // Assert
            Assert.True(dispatchResult.IsConcurrencyIssue());
        }

        [Fact]
        public async Task UpdateProductDispatchesEventsTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var updateCommand = new ProductUpdateCommand(createCommand.Id, concurrencyToken, "def", 234);
            MessageRecorder.Clear();

            // Act
            await MessageDispatcher.DispatchAsync(updateCommand);

            // Assert
            Assert.Contains(MessageRecorder.RecordedMessages, dispatchData =>
            {
                if (dispatchData.MessageType != typeof(ProductUpdated))
                    return false;

                var evt = dispatchData.Message as ProductUpdated;

                Assert.Equal(updateCommand.Id, evt.Id);
                Assert.Equal(updateCommand.Name, evt.Name);
                Assert.Equal(updateCommand.Price, evt.Price);

                return true;
            });
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
            var query = new ByIdQuery<ProductModel>(updateCommand.Id);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResult<ProductModel>(out var productModel));
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
            var query = new Query<ProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProductListModel>(out var listModels));
            Assert.Collection(listModels, listModel =>
            {
                Assert.Equal(updateCommand.Id, listModel.Id);
                Assert.Equal(updateCommand.Name, listModel.Name);
                Assert.Equal(updateCommand.Price, listModel.Price);
            });
        }

        [Fact]
        public async Task DeleteProductIsSuccessTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var deleteCommand = new ProductDeleteCommand(createCommand.Id, concurrencyToken);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(deleteCommand);

            // Assert
            Assert.True(dispatchResult.IsSuccess);
        }

        [Fact]
        public async Task DeleteProductWithDefaultConcurrencyTokenFailsTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(createCommand);
            var deleteCommand = new ProductDeleteCommand(createCommand.Id, string.Empty);

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(deleteCommand);

            // Assert
            Assert.True(dispatchResult.IsConcurrencyIssue());
        }

        [Fact]
        public async Task DeleteProductWithWrongConcurrencyTokenFailsTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            await MessageDispatcher.DispatchAsync(createCommand);
            var deleteCommand = new ProductDeleteCommand(createCommand.Id, Guid.NewGuid().ToString());

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(deleteCommand);

            // Assert
            Assert.True(dispatchResult.IsConcurrencyIssue());
        }

        [Fact]
        public async Task DeleteProductDispatchesEventsTest()
        {
            // Arrange
            var createCommand = new ProductCreateCommand(Guid.NewGuid(), "abc", 123);
            var createResult = await MessageDispatcher.DispatchAsync(createCommand);
            var concurrencyToken = createResult.ResultData["ConcurrencyToken"]?.ToString();
            var deleteCommand = new ProductDeleteCommand(createCommand.Id, concurrencyToken);
            MessageRecorder.Clear();

            // Act
            await MessageDispatcher.DispatchAsync(deleteCommand);

            // Assert
            Assert.Contains(MessageRecorder.RecordedMessages, dispatchData =>
            {
                if (dispatchData.MessageType != typeof(ProductDeleted))
                    return false;

                var evt = dispatchData.Message as ProductDeleted;

                Assert.Equal(deleteCommand.Id, evt.Id);

                return true;
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
            var query = new ByIdQuery<ProductModel>(deleteCommand.Id);

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
            var query = new Query<ProductListModel>();

            // Act
            var dispatchResult = await MessageDispatcher.DispatchAsync(query);

            // Assert
            Assert.True(dispatchResult.IsSuccessWithResults<ProductListModel>(out var listModels));
            Assert.Empty(listModels);
        }
    }
}
