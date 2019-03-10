using System.Threading.Tasks;
using AI4E.Blazor;
using Microsoft.AspNetCore.Blazor.Hosting;

namespace BookStore.App
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var wasmHost = CreateHostBuilder(args).Build();
            wasmHost.Run();
            wasmHost.InitializeApplicationServices();
        }

        private static IWebAssemblyHostBuilder CreateHostBuilder(string[] args)
        {
            return BlazorWebAssemblyHost.CreateDefaultBuilder()
                 .UseBlazorStartup<Startup>();
        }
    }
}