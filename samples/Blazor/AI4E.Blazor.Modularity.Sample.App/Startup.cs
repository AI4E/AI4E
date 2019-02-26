using AI4E.Blazor.Routing;
using Microsoft.AspNetCore.Components.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity.Sample.App
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                //builder.AddBrowserConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            services.AddBlazorModularity();
        }

        public void Configure(IComponentsApplicationBuilder app)
        {
            app.AddComponent<RouterX>("app");
        }
    }
}
