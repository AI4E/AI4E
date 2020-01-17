using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Routing.Modularity.Sample
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            await host.InitializeApplicationServicesAsync();
            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                });
        }
    }
}
