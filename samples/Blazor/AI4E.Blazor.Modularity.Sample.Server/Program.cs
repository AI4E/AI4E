using AI4E.Routing.SignalR.Server;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity.Sample.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webhost = BuildWebHost(args);

            // Read the message dispatcher from the services in order to register all available handlers.
            webhost.Services.GetRequiredService<IMessageDispatcher>();

            webhost.Services.GetRequiredService<ClientManager>();

            webhost.Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                          
                          .ConfigureLogging((hostingContext, logging) =>
                          {
                              logging.SetMinimumLevel(LogLevel.Debug);
                              logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                              logging.AddConsole();
                              logging.AddDebug();
                          })
                          .UseConfiguration(new ConfigurationBuilder()
                            .AddCommandLine(args)
                            .Build())
                          .UseStartup<Startup>()
                          .Build();
        }
    }
}
