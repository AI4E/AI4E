using System.Threading.Tasks;
using AI4E.AspNetCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI4E.Routing.Blazor.Sample.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var webhost = BuildWebHost(args);
            await webhost.InitializeApplicationServicesAsync();
            await webhost.RunAsync();
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
