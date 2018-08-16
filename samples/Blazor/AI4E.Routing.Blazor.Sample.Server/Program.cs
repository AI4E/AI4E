using AI4E.Routing.SignalR.Server;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Routing.Blazor.Sample.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webHost = BuildWebHost(args);

            // Read the message dispatcher from the services in order to register all available handlers.
            webHost.Services.GetRequiredService<IMessageDispatcher>();

            webHost.Services.GetRequiredService<ClientManager>();

            webHost.Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                           .UseUrls("http://*:5050")
                          .ConfigureLogging((hostingContext, logging) =>
                          {
                              logging.SetMinimumLevel(LogLevel.Trace);
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
