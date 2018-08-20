using System.Linq;
using System.Net.Mime;
using AI4E.Blazor.Server;
using AI4E.Modularity.Module;
using AI4E.Storage;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Blazor.Modularity.Sample.Module.Server
{
    public class Startup
    {
        private static readonly string _prefix = "module";

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    MediaTypeNames.Application.Octet,
                    WasmMediaTypeNames.Application.Wasm,
                });
            });

            services.AddStorage()
                    .UseMongoDB(options =>
                    {
                        options.ConnectionString = "mongodb://localhost:27017";
                        options.Database = "AI4E-Blazor-Sample-DB";
                    });

            services.Configure<ModuleServerOptions>(options =>
            {
                options.Prefix = _prefix;
                options.UseDebugConnection = true;
                options.DebugConnection = "localhost:8080";
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UsePathBase("/" + _prefix);
            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseBlazorModule<App._ViewImports>();
        }
    }
}
