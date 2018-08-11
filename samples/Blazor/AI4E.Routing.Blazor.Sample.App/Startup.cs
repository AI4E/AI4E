using System;
using Blazor.Extensions;
using Microsoft.AspNetCore.Blazor.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.Routing.Blazor.Sample.App
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            services.AddSingleton(ConfigureHubConnection);
            services.AddBlazorMessageDispatcher();
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
