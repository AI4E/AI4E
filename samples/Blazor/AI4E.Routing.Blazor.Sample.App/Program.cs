using Microsoft.AspNetCore.Blazor.Hosting;

namespace AI4E.Routing.Blazor.Sample.App
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            host.Run();
        }

        public static IWebAssemblyHostBuilder CreateHostBuilder(string[] args)
        {
            return BlazorWebAssemblyHost.CreateDefaultBuilder()
                                        .UseBlazorStartup<Startup>();
        }
    }
}
