using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Modularity;
using AI4E.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
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
            services.AddSingleton<IBlazorModuleSource>(p => p.GetRequiredService<PluginManager>());
            services.Decorate<IBlazorModuleSource>(p => p.Configure(ProcessModuleDescriptor));

            var entryAssembly = Assembly.GetEntryAssembly() ?? typeof(Startup).Assembly;

            services.AddBlazorModularity(entryAssembly);

            services.AddMessaging();

            services.AddServerSideBlazor();
            services.AddAutofacChildContainerBuilder();
        }

        private IBlazorModuleDescriptor ProcessModuleDescriptor(IBlazorModuleDescriptor moduleDescriptor)
        {
            var moduleBuilder = moduleDescriptor.ToBuilder();
            var newtonsoftJsonAssembly = typeof(JsonSerializer).Assembly;
            var newtonsoftJsonAssemblyName = newtonsoftJsonAssembly.GetName();
            var newtonsoftJsonAssemblyDescriptor = moduleBuilder.Assemblies.FirstOrDefault(
                p => AssemblyNameComparer.BySimpleName.Equals(p.GetAssemblyName(), newtonsoftJsonAssemblyName));

            if (newtonsoftJsonAssemblyDescriptor is null)
            {
                newtonsoftJsonAssemblyDescriptor = BlazorModuleAssemblyDescriptor.CreateBuilder(
                    newtonsoftJsonAssembly, forceLoad: true);

                moduleBuilder.Assemblies.Add(newtonsoftJsonAssemblyDescriptor);
            }
            else
            {
                var loadAssemblySourceAsync = newtonsoftJsonAssemblyDescriptor.LoadAssemblySourceAsync;

                async ValueTask<BlazorModuleAssemblySource> InternalLoadAssemblySourceAsync(CancellationToken cancellation)
                {
                    var source = await loadAssemblySourceAsync(cancellation);
                    return source.Configure(forceLoad: true);
                }

                newtonsoftJsonAssemblyDescriptor.LoadAssemblySourceAsync = InternalLoadAssemblySourceAsync;
            }

            return moduleBuilder.Build();
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
