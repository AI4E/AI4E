using AI4E.Domain.Services;
using AI4E.Modularity;
using AI4E.Modularity.Host;
using AI4E.Routing;
using AI4E.Storage;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Host
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddStorage()
                    .UseMongoDB(options =>
                    {
                        options.ConnectionString = "mongodb://localhost:27017";
                        options.Database = "AI4E-DB";
                    });

            services.AddModularity(options =>
            {
                options.DebugConnection = "localhost:8080";
                options.EnableDebugging = true;
            });

            services.Configure<RemoteMessagingOptions>(options =>
            {
                options.LocalEndPoint = EndPointRoute.CreateRoute("host");
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseModularity();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "area",
                    template: "{area:exists}/{controller=Default}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
