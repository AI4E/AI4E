using System;
using System.Threading.Tasks;
using AI4E.Storage.Domain;
using AI4E.Storage.InMemory;
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
        }

        private static async Task RunAsync()
        {
            var entityStorageEngine = ServiceProvider.GetRequiredService<IEntityStorageEngine>();
        }
    }
}
