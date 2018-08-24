using System;
using AI4E.Storage.Domain;
using AI4E.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Utils
{
    public static class Setup
    {
        public static IServiceProvider BuildDefaultInMemorySetup()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddInMemoryMessaging();
            services.AddStorage()
                    .UseInMemoryDatabase()
                    .UseDomainStorage();

            return services.BuildServiceProvider();
        }
    }
}
