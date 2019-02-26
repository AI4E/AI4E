using AI4E.Modularity.Host;
using AI4E.Routing.SignalR.Server;
using AI4E.Storage;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor.Modularity.Sample.Server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds the Server-Side Blazor services, and those registered by the app project's startup.
            //services.AddServerSideBlazor<App.Startup>();

            services.AddResponseCompression();

            services.AddSignalRServerRouting();

            services.AddStorage()
                    .UseMongoDB(options =>
                    {
                        options.ConnectionString = "mongodb://localhost:27017";
                        options.Database = "AI4E-Blazor-Sample-DB";
                    });

            services.AddModularity(options =>
            {
                options.EnableDebugging = true;
                options.DebugConnection = "localhost:8080";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseModularity();

            app.UseSignalRServerRouting();

            // Use component registrations and static files from the app project.
            app.UseBlazor<App.Startup>();/*ServerSide*/
        }
    }
}
