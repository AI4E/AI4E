using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Domain.Services;
using AI4E.Storage.Domain;
using AI4E.Storage.InMemory;
using AI4E.Storage.Sample.Api;
using AI4E.Storage.Sample.Domain;
using AI4E.Storage.Sample.Models;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Sample
{
    public static class Program
    {
        private static IServiceProvider ServiceProvider { get; set; }

        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            await RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddInMemoryMessaging();

            services.AddStorage()
                    .UseInMemoryDatabase()
                    .UseDomainStorage();

            services.AddDomainServices();
        }

        private static async Task RunAsync()
        {
            var messageDispatcher = ServiceProvider.GetRequiredService<IMessageDispatcher>();
            var dataStore = ServiceProvider.GetRequiredService<IDataStore>();

            var command = new ProductCreateCommand(Guid.NewGuid(), "myProduct");
            var commandResult = await messageDispatcher.DispatchAsync(command);

            await Console.Out.WriteLineAsync(commandResult.ToString());

            var product = await LoadProductUncachedAsync(command.Id);

            await Console.Out.WriteLineAsync(product.ProductName);

            var listModel = await LoadProductListModelUncachedAsync(command.Id);

            await Console.Out.WriteLineAsync(listModel.ProductName);

            var deleteModel = await dataStore.FindOneAsync<ProductDeleteModel>(p => p.Id == command.Id);

            var deleteCommand = new ProductDeleteCommand(deleteModel.Id, deleteModel.ConcurrencyToken);
            var deleteCommandResult = await messageDispatcher.DispatchAsync(deleteCommand);
            product = await LoadProductUncachedAsync(command.Id);

            await Console.Out.WriteLineAsync($"Product is{(product == null ? "" : " not")} deleted.");

            listModel = await LoadProductListModelUncachedAsync(command.Id);

            await Console.Out.WriteLineAsync($"Product is{(listModel == null ? "" : " not")} deleted.");

            await Console.In.ReadLineAsync();
        }

        private static async Task<Product> LoadProductUncachedAsync(Guid id)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>();
                return await storageEngine.GetByIdAsync(typeof(Product), id.ToString()) as Product;
            }
        }

        private static async Task<ProductListModel> LoadProductListModelUncachedAsync(Guid id)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var dataStore = scope.ServiceProvider.GetRequiredService<IDataStore>();
                return await dataStore.FindOneAsync<ProductListModel>(p => p.Id == id);
            }
        }
    }
}
