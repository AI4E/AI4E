using System.Threading.Tasks;
using AI4E.AspNetCore;
using AI4E.Modularity.Module;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookStore.Catalog.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var webhost = WebHost
                .CreateDefaultBuilder(args)
                .UseModuleServer(module => module.UseDebugging())
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.SetMinimumLevel(LogLevel.Debug);
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .UseStartup<Startup>()
                .Build();

            await webhost.InitializeApplicationServicesAsync(cancellation: default);
            webhost.Run();
        }
    }
}
