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
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddNewtonsoftJson();

            services.AddSingleton<PluginManager>();
            services.AddSingleton<IBlazorModuleSource>(p => p.GetRequiredService<PluginManager>());
            services.AddBlazorModularity(entryAssembly: Assembly.GetEntryAssembly() ?? typeof(Startup).Assembly);

            services.AddMessaging();

            services.AddServerSideBlazor();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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
