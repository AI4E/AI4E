using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Host;
using AI4E.Storage;
using AI4E.Storage.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Routing.SignalR.Server.Sample
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

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

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
            var clientManager = app.ApplicationServices.GetRequiredService<ClientManager>();

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

            app.UseSignalR(routes =>
            {
                routes.MapHub<ServerCallStub>("/MessageDispatcherHub");
            });

            app.UseMvc();
        }
    }
}
