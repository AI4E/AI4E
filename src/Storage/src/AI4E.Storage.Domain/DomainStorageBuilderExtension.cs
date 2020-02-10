using Microsoft.Extensions.DependencyInjection;
using System;

namespace AI4E.Storage.Domain
{
    public static class DomainStorageBuilderExtension
    {
        public static IDomainStorageBuilder Configure(
            this IDomainStorageBuilder builder,
            Action<DomainStorageOptions> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            builder.Services.Configure(configuration);
#pragma warning restore CA1062

            return builder;
        }
    }
}
