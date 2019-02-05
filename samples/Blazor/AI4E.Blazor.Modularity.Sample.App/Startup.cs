using AI4E.Blazor.Routing;
using Blazor.Extensions.Logging;
using Microsoft.AspNetCore.Blazor.Builder;
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
                builder.AddBrowserConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            services.AddBlazorModularity(isServerSide: false);
        }

        public void Configure(IBlazorApplicationBuilder app)
        {
            app.AddComponent<RouterX>("app");
        }
    }
}
