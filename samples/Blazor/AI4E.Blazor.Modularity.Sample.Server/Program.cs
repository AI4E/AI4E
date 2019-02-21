using System.Threading.Tasks;
using AI4E.AspNetCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity.Sample.Server
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

                          .ConfigureLogging((hostingContext, logging) =>
                          {
                              logging.SetMinimumLevel(LogLevel.Information);
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
