using System.Reflection;
using AI4E.AspNetCore.Components.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Routing.Modularity.Sample.Services;

namespace Routing.Modularity.Sample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddNewtonsoftJson();

            services.AddSingleton<PluginManager>();

            services.AddMessaging();
            services.AddBlazorModularity(Assembly.GetEntryAssembly() ?? typeof(Startup).Assembly)
                    .UseModuleSourceFactory<PluginModuleSourceFactory>()
                    .UseModuleMessaging();

            services.AddServerSideBlazor();
            services.AddAutofacChildContainerBuilder();
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/Index");
            });
        }
    }
}
