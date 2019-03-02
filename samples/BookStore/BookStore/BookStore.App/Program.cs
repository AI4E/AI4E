using Microsoft.AspNetCore.Blazor.Hosting;

namespace BookStore.App
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        private static IWebAssemblyHostBuilder CreateHostBuilder(string[] args)
        {
            return BlazorWebAssemblyHost.CreateDefaultBuilder()
                 .UseBlazorStartup<Startup>();
        }
    }
}
