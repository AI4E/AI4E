using AI4E.Domain.Services;
using AI4E.Modularity.Host;
using AI4E.Routing;
using AI4E.Storage;
using AI4E.Storage.Domain;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity.Hosting.Sample
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
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc()
                    .AddModuleModelBinders()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddStorage()
                    //.UseInMemoryDatabase()
                    .UseMongoDB(options => options.Database = "AI4EHostingSampleDB")
                    .UseDomainStorage();

            services.AddModularity();
            services.AddDomainServices();

            services.Configure<RemoteMessagingOptions>(options => options.LocalEndPoint = new EndPointAddress("AI4E.Modularity.Hosting.Sample"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseModularity();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "area",
                    template: "{area:exists}/{controller=default}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "default",
                    template: "{controller=default}/{action=Index}/{id?}");
            });
        }
    }
}
