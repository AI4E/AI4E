using AI4E.Modularity.Host;
using AI4E.Routing.SignalR.Server;
using AI4E.Storage;
using AI4E.Storage.InMemory;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Net.Mime;

namespace AI4E.Routing.Blazor.Sample.Server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds the Server-Side Blazor services, and those registered by the app project's startup.
            //services.AddServerSideBlazor<App.Startup>();

            services.AddResponseCompression(options =>
            {
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    MediaTypeNames.Application.Octet,
                    WasmMediaTypeNames.Application.Wasm,
                });
            });

            services.AddStorage()
                    .UseInMemoryDatabase();

            services.AddModularity();
            services.AddSignalR();

            services.AddSingleton<ServerEndPoint>();
            services.AddSingleton<IServerEndPoint>(p => p.GetRequiredService<ServerEndPoint>());
            services.AddSingleton<ILogicalServerEndPoint, LogicalServerEndPoint>();
            services.AddSingleton<ClientManager>();
            services.AddSingleton<IConnectedClientLookup, ConnectedClientLookup>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSignalR(routes =>
            {
                routes.MapHub<ServerCallStub>("/MessageDispatcherHub");
            });

            // Use component registrations and static files from the app project.
            //app.UseServerSideBlazor<App.Startup>();

            app.UseBlazor<App.Startup>();
        }
    }
}
