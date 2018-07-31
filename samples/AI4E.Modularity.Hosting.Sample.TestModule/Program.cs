using System;
using System.Net;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Remoting;
using AI4E.Routing;
using AI4E.Storage;
using AI4E.Storage.Domain;
using AI4E.Storage.MongoDB;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity.Hosting.Sample.TestModule
{
    internal static class Program
    {
        private static IServiceProvider ServiceProvider { get; set; }

        internal static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            await RunAsync();
        }
        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddStorage()
                    .UseMongoDB(options => options.Database = "AI4EHostingSampleDB")
                    .UseDomainStorage();

            services.AddOptions();
            services.AddMessageDispatcher<IRemoteMessageDispatcher, RemoteMessageDispatcher>();
            services.AddSingleton<IAddressConversion<IPEndPoint>, IPEndPointSerializer>();
            services.AddSingleton<IRouteSerializer, EndPointRouteSerializer>();
            services.AddSingleton<ITypeConversion, TypeSerializer>();
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<IRouteStore, RouteManager>();
            services.AddSingleton<IRouteMap<IPEndPoint>, RouteMap<IPEndPoint>>();

            services.AddSingleton(ConfigurePhysicalEndPoint);
            services.AddSingleton(ConfigureEndPointManager);
            services.AddSingleton(ConfigureCoordinationManager);
            services.AddSingleton(ConfigureLogicalEndPoint);
        }

        private static IPhysicalEndPoint<IPEndPoint> ConfigurePhysicalEndPoint(IServiceProvider serviceProvider)
        {
            return ActivatorUtilities.CreateInstance<UdpEndPoint>(serviceProvider);
        }

        private static ICoordinationManager ConfigureCoordinationManager(IServiceProvider serviceProvider)
        {
            return ActivatorUtilities.CreateInstance<CoordinationManager<IPEndPoint>>(serviceProvider);
        }

        private static IEndPointManager ConfigureEndPointManager(IServiceProvider serviceProvider)
        {
            return ActivatorUtilities.CreateInstance<EndPointManager<IPEndPoint>>(serviceProvider);
        }

        private static ILogicalEndPoint ConfigureLogicalEndPoint(IServiceProvider serviceProvider)
        {
            var endPointManager = serviceProvider.GetRequiredService<IEndPointManager>();
            return endPointManager.GetLogicalEndPoint(EndPointRoute.CreateRoute("AI4E.Modularity.Hosting.Sample.TestModule"));
        }

        private static async Task RunAsync()
        {
            var dispatcher = ServiceProvider.GetRequiredService<IMessageDispatcher>();

            await Task.Delay(-1);
        }

    }
}
