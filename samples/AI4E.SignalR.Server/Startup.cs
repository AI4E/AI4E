using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E.SignalR.Server.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AI4E.SignalR.Server.Infrastructure;
using AI4E.SignalR.Server.Abstractions;
using AI4E.Routing;
using AI4E.Domain.Services;
using AI4E.Modularity;
using AI4E.Storage.MongoDB;

namespace AI4E.SignalR.Server
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
                    .UsingMongoDBPersistence(options =>
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
          
            services.AddSignalR();
            services.AddSingleton<IClientLogicalEndPointAssociationStorage, ClientLogicalEndPointAssociationInMemoryStorage>();
            services.AddSingleton<IClientRemoteMessageDispatcherAssociationStorage, ClientRemoteMessageDispatcherAssociationInMemoryStorage>();
            
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
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseSignalR(routes =>
            {
                routes.MapHub<MessageDispatcherHub>("/MessageDispatcherHub");
            });
            app.UseMvc();
        }
    }
}
