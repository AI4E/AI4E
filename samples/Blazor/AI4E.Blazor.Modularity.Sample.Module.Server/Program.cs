using AI4E.Modularity.Module;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity.Sample.Module.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webHost = ModuleWebHost.CreateModuleBuilder(args)
                 .ConfigureLogging((hostingContext, logging) =>
                 {
                     logging.SetMinimumLevel(LogLevel.Trace);
                     logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                     logging.AddConsole();
                     logging.AddDebug();
                 })
                                        .UseStartup<Startup>()
                                        .Build();

            webHost.Run();
        }
    }
}
