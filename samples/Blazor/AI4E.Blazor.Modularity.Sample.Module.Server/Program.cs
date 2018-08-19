using AI4E.Modularity.Module;
using Microsoft.AspNetCore.Hosting;

namespace AI4E.Blazor.Modularity.Sample.Module.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var webHost = ModuleWebHost.CreateModuleBuilder(args)
                                        .UseStartup<Startup>()
                                        .Build();

            webHost.Run();
        }
    }
}
