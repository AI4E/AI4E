using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AI4E;
using AI4E.Domain;
using AI4E.Domain.Services;
using AI4E.Storage.MongoDB;
using AI4E.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Products
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
            services.AddInMemoryMessaging(options =>
            {
                options.MessageProcessors.Add(ContextualProvider.Create<EntityMessageHandlerProcessor<Guid, DomainEvent, AggregateRoot>>());
                options.MessageProcessors.Add(ContextualProvider.Create<ValidationCommandProcessor>());
            });
            services.AddStorage().UsingMongoDBPersistence(options => 
            {
                options.ConnectionString = "mongodb://127.0.0.1:27017";
                options.Database = "Products";
            });

            services.AddDomainServices();
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

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Product}/{action=List}/{id?}");
            });
        }
    }
}
