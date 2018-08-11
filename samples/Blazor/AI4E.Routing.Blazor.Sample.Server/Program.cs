using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Routing.Blazor.Sample.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webHost = BuildWebHost(args);

            // Read the message dispatcher from the services in order to register all available handlers.
            webHost.Services.GetRequiredService<IMessageDispatcher>();

            webHost.Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                          .UseConfiguration(new ConfigurationBuilder()
                          .AddCommandLine(args)
                          .Build())
                          .UseStartup<Startup>()
                          .Build();
        }
    }
}
