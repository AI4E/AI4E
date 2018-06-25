using System;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Routing
{
    public static class EndPointManagerServiceCollectionExtension
    {
        public static void AddEndPointManager<TAddress>(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton(typeof(IMessageCoder<TAddress>), typeof(MessageCoder<TAddress>));
            services.AddSingleton(typeof(EndPointManager<TAddress>));
            services.AddSingleton(typeof(IEndPointManager<TAddress>), provider => provider.GetRequiredService<EndPointManager<TAddress>>());
            services.AddSingleton(typeof(IEndPointManager), provider => provider.GetRequiredService<EndPointManager<TAddress>>());
        }
    }
}
