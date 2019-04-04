using AI4E.Domain.Services;
using AI4E.Modularity.Host;
using AI4E.Routing.SignalR.Server;
using AI4E.Storage;
using AI4E.Storage.Domain;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookStore.Server
{
    public class Startup
    {
        private IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression();
            services.AddSingleton<ServerSideIndicator>(); // TODO: This should be moved from the sample into the lib.
            services.AddSignalRServerRouting();
            services.AddMvc().AddNewtonsoftJson();

            services.AddStorage()
                    .UseMongoDB(useNativeTransactions: true)
                    .UseDomainStorage();

            services.AddDomainServices();
            services.AddModularity();

            // Bind Configuration
            services.Configure<MongoOptions>(Configuration.GetSection("MongoDB"));
            services.Configure<ModularityOptions>(Configuration.GetSection("Modularity"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        [System.Obsolete] // TODO: Fixme pls
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBlazorDebugging();
            }

            app.UseStaticFiles();
            app.UseSignalRServerRouting();
            app.UseModularity();
            //app.UseSignalR(route => route.MapHub<BlazorHub>(BlazorHub.DefaultPath));
            app.UseBlazor<App.Startup>();
        }
    }
}
