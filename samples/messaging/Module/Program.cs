using AI4E.Modularity;
using Microsoft.AspNetCore.Hosting;

namespace Module
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
