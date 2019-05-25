using System.Linq;
using System.Reflection;
using AI4E.AspNetCore.Components.Modularity;
using AI4E.Utils.ApplicationParts;
using Microsoft.AspNetCore.Components.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookStore.App
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var isServerSide = services.Any(p => p.ServiceType == typeof(ServerSideIndicator));

            if (!isServerSide)
            {
                services.AddLogging(builder =>
                {
                    builder.AddBrowserConsole();
                    builder.SetMinimumLevel(LogLevel.Debug);
                });
            }

            services.ConfigureApplicationParts(ConfigureApplicationParts);
            services.AddBlazorModularity(typeof(Startup).Assembly/*, isServerSide*/);
            services.AddSharedBookStoreServices();
        }

        private static void ConfigureApplicationParts(ApplicationPartManager partManager)
        {
            partManager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));
        }

        public void Configure(IComponentsApplicationBuilder app)
        {
            app.AddComponent<App>("app");
        }
    }
}
