using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using AI4E.AspNetCore;

namespace AI4E.Modularity.Hosting.Sample
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var webhost = CreateWebHostBuilder(args).Build();

            await webhost.InitializeApplicationServicesAsync();
            await webhost.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                          .UseStartup<Startup>();
        }
    }
}
