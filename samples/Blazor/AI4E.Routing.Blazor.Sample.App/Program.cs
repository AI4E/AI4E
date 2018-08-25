using AI4E.Blazor;
using Microsoft.AspNetCore.Blazor.Hosting;

namespace AI4E.Routing.Blazor.Sample.App
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var wasmHost = CreateHostBuilder(args).Build();

            wasmHost.Run();
            wasmHost.InitializeApplicationServices();
        }

        public static IWebAssemblyHostBuilder CreateHostBuilder(string[] args)
        {
            return BlazorWebAssemblyHost.CreateDefaultBuilder()
                                        .UseBlazorStartup<Startup>();
        }
    }
}
