using System;
using AI4E.Messaging;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace Routing.Modularity.Sample.PluginA
{
    public sealed class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMessaging(suppressRoutingSystem: true);
            services.Configure<MessagingOptions>(
                      options => options.LocalEndPoint = new RouteEndPointAddress(Guid.NewGuid().ToString()));
            services.AddSingleton<IMessageSerializer, MessageSerializer>();
        }
    }
}
