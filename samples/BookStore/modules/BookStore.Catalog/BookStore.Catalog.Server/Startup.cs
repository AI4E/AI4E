using AI4E.AspNetCore.Components.ModuleServer;
using AI4E.Domain.Services;
using AI4E.Modularity.Debug;
using AI4E.Modularity.Module;
using AI4E.Storage;
using AI4E.Storage.Domain;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookStore.Catalog.Server
{
    public class Startup
    {
        private const string _prefix = "catalog";

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression();

            services.AddStorage()
                    .UseMongoDB(database: "BookStoreDatabase")
                    .UseDomainStorage();

            services.AddDomainServices();
            services.AddModuleServices();

            services.Configure<ModuleServerOptions>(options =>
            {
                options.Prefix = _prefix;
            });

            services.Configure<ModuleDebugOptions>(options =>
            {
                options.UseDebugConnection = true;
                options.DebugConnection = "localhost:8080";
            });

            services.AddBlazorModuleServer(typeof(App.Startup).Assembly);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UsePathBase("/" + _prefix);

            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseBlazorModule<App.Startup>();
        }
    }
}
