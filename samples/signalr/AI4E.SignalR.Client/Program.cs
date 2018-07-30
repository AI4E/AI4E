using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace AI4E.SignalR.Client
{
    internal static class Program
    {
        internal static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            using (var serviceProvider = services.BuildServiceProvider())
            {
                var persistentConnection = serviceProvider.GetRequiredService<IPersistentConnection>();

                await RunAsync(persistentConnection);
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddHubConnection()
                    .WithUrl("http://localhost:5000/MessageDispatcherHub")
                    .Build();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            services.AddSingleton<IPersistentConnection, SignalRClientConnection>();

            //services.AddMessageDispatcher<IRemoteMessageDispatcher, FrontEndMessageDispatcher>();
        }

        private static async Task RunAsync(IPersistentConnection persistentConnection)
        {
            await Task.Delay(1000);

            await Console.In.ReadLineAsync();
        }
    }
}
