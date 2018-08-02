using System;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Routing.FrontEnd
{
    public static class ServiceCollectionExtension
    {
        public static IHubConnectionBuilder AddHubConnection(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return new HubConnectionBuilder(services);
        }
    }
}
