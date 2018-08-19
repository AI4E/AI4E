using System.Linq;
using System.Net.Mime;
using AI4E.Blazor.Modularity.Sample.Module.App;
using AI4E.Modularity.Module;
using AI4E.Storage;
using AI4E.Storage.MongoDB;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

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

            var appPath = @"C:\Users\at\Source\Repos\AI4E_feature_blazor\artifacts\bin\AI4E.Blazor.Modularity.Sample.Module.App\Debug\netstandard2.0";

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(appPath),
                ContentTypeProvider = CreateContentTypeProvider(/*config.EnableDebugging*/true),
                OnPrepareResponse = SetCacheHeaders
            });
        }

        private static IContentTypeProvider CreateContentTypeProvider(bool enableDebugging)
        {
            var result = new FileExtensionContentTypeProvider();
            result.Mappings.Add(".dll", MediaTypeNames.Application.Octet);
            result.Mappings.Add(".mem", MediaTypeNames.Application.Octet);
            result.Mappings.Add(".wasm", WasmMediaTypeNames.Application.Wasm);

            if (enableDebugging)
            {
                result.Mappings.Add(".pdb", MediaTypeNames.Application.Octet);
            }

            return result;
        }

        private static void SetCacheHeaders(StaticFileResponseContext ctx)
        {
            // By setting "Cache-Control: no-cache", we're allowing the browser to store
            // a cached copy of the response, but telling it that it must check with the
            // server for modifications (based on Etag) before using that cached copy.
            // Longer term, we should generate URLs based on content hashes (at least
            // for published apps) so that the browser doesn't need to make any requests
            // for unchanged files.
            var headers = ctx.Context.Response.GetTypedHeaders();
            if (headers.CacheControl == null)
            {
                headers.CacheControl = new CacheControlHeaderValue
                {
                    NoCache = true
                };
            }
        }
    }
}
