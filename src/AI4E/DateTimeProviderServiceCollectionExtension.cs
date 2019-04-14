using System;
using AI4E;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class DateTimeProviderServiceCollectionExtension
    {
        public static IServiceCollection AddDateTimeProvider(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<IDateTimeProvider, DateTimeProvider>();

            return services;
        }
    }
}
