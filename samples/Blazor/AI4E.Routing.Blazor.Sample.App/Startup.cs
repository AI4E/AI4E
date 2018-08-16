using System;
using System.Linq;
using System.Reflection;
using AI4E.Blazor.ApplicationParts;
using Blazor.Extensions;
using Blazor.Extensions.Logging;
using Microsoft.AspNetCore.Blazor.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.Blazor.Sample.App
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                //builder.AddConsole();
                builder.AddBrowserConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            services.AddSingleton(ConfigureHubConnection);
            services.AddBlazorMessageDispatcher();

            var manager = GetService<ApplicationPartManager>(services);

            Assert(manager != null);

            manager.ApplicationParts.Add(new AssemblyPart(Assembly.GetExecutingAssembly()));

        }

        public static T GetService<T>(IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var serviceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(T));

            return (T)serviceDescriptor?.ImplementationInstance;
        }

        private HubConnection ConfigureHubConnection(IServiceProvider serviceProvider)
        {
            var connection = new HubConnectionBuilder()
                                .WithUrl("/MessageDispatcherHub", opt =>
                                {
                                    opt.LogLevel = SignalRLogLevel.Trace;
                                    opt.Transport = HttpTransportType.WebSockets;
                                })
                                .Build();

            return connection;
        }

        public void Configure(IBlazorApplicationBuilder app)
        {
            app.AddComponent<App>("app");
        }
    }
}
