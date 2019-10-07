using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging.SignalR.Server
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddSignalRServerRouting(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSignalR().AddNewtonsoftJsonProtocol();
            services.AddSingleton<SignalRServerEndPoint>();
            services.AddSingleton<ISignalRServerEndPoint>(p => p.GetRequiredService<SignalRServerEndPoint>());
            services.AddSingleton<ClientManager>();
            services.AddSingleton<IClientConnectionManager, ClientConnectionManager>();
            services.ConfigureApplicationServices(serviceManager => serviceManager.AddService<ClientManager>());

            return services;
        }
    }

    public static class ApplicationBuilderExtension
    {
        internal static PathString DefaultHubPath { get; } = "/MessageDispatcherHub";

        public static IApplicationBuilder UseSignalRServerRouting(this IApplicationBuilder app)
        {
            return UseSignalRServerRouting(app, DefaultHubPath);
        }

        public static IApplicationBuilder UseSignalRServerRouting(this IApplicationBuilder app, PathString path)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            app.UseSignalR(routes =>
            {
                routes.MapHub<SignalRServerEndPoint.ServerCallStub>(path);
            });

            return app;
        }

        public static IApplicationBuilder UseSignalRServerRouting(this IApplicationBuilder app, PathString path, Action<HttpConnectionDispatcherOptions> configureOptions)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            app.UseSignalR(routes =>
            {
                routes.MapHub<SignalRServerEndPoint.ServerCallStub>(path, configureOptions);
            });

            return app;
        }
    }
}
