using System;
using System.Threading.Tasks;
using AI4E.Storage.InMemory;
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
                    .UseInMemoryDatabase();
        }

        private static async Task RunAsync()
        {
            var entityStore = ServiceProvider.GetRequiredService<IEntityStore>();
        }
    }
}
