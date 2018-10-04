using System;
using AI4E.Remoting;
using AI4E.Storage;
using AI4E.Storage.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Coordination.Utils
{
    public static class Setup
    {
        public static IServiceProvider BuildDefaultInMemorySetup()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddStorage()
                    .UseInMemoryDatabase();

            services.AddPhysicalEndPoint<InMemoryPhysicalAddress, InMemoryPhysicalEndPoint>();
            services.AddSingleton<IAddressConversion<InMemoryPhysicalAddress>, InMemoryPhysicalAddressConversion>();

            services.AddCoordinationService();

            return services.BuildServiceProvider();
        }
    }
}
