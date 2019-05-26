using System.Threading.Tasks;
using AI4E.AspNetCore;
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
                .UseModuleServer()
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
