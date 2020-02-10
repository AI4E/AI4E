using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AI4E.Messaging;
using AI4E.Storage.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Storage
{
    public static class StorageBuilderExtension
    {
        private static IDomainStorageBuilder InternalUseDomainStorage(this IStorageBuilder builder)
        {
            if (!TryGetDomainStorageBuilder(builder, out var domainStorageBuilder))
            {
                var services = builder.Services;
                services.TryAddSingleton<IMessageAccessor, DefaultMessageAccessor>();

                // TODO: Add default entity storage engine

                AddMessageProcessors(services);

                domainStorageBuilder = new DomainStorageBuilder(builder);
                builder.Services.AddSingleton(domainStorageBuilder);
            }

            return domainStorageBuilder;
        }

        private static bool TryGetDomainStorageBuilder(
            this IStorageBuilder builder,
            [NotNullWhen(true)] out DomainStorageBuilder? domainStorageBuilder)
        {
            domainStorageBuilder = builder.Services.LastOrDefault(
                p => p.ServiceType == typeof(DomainStorageBuilder))?.ImplementationInstance as DomainStorageBuilder;

            return domainStorageBuilder != null;
        }

        public static IStorageBuilder UseDomainStorage(this IStorageBuilder builder)
        {
#pragma warning disable CA1062
            _ = InternalUseDomainStorage(builder);
#pragma warning restore CA1062
            return builder;
        }

        public static IStorageBuilder UseDomainStorage(this IStorageBuilder builder, Action<IDomainStorageBuilder> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            var domainStorageBuilder = InternalUseDomainStorage(builder);
#pragma warning restore CA1062
            configuration(domainStorageBuilder);
            return builder;
        }

        private static void AddMessageProcessors(IServiceCollection services)
        {
            services.AddMessaging(options =>
            {
                options.MessageProcessors.Add(MessageProcessorRegistration.Create<EntityMessageHandlerProcessor>());
            });
        }
    }
}
