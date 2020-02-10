using System;
using AI4E.Messaging;
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    public static class ProjectionDomainStorageBuilderExtension
    {
        public static IDomainStorageBuilder AddProjection(this IDomainStorageBuilder builder)
        {
#pragma warning disable CA1062
            return InternalAddProjection(builder, configuration: null);
#pragma warning restore CA1062
        }

        public static IDomainStorageBuilder AddProjection(
            this IDomainStorageBuilder builder,
            Action<IProjectionBuilder> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CA1062
            return InternalAddProjection(builder, configuration);
#pragma warning restore CA1062
        }

        private static IDomainStorageBuilder InternalAddProjection(
           IDomainStorageBuilder builder,
           Action<IProjectionBuilder>? configuration)
        {
#pragma warning disable CA1062
            builder.StorageBuilder.AddProjection(
#pragma warning restore CA1062
                BuildProjectionConfiguration(configuration));

            builder.Services.AddMessaging().ConfigureMessageHandlers((registry, _) =>
            {
                registry.Register(new MessageHandlerRegistration<ProjectEntityMessage>(
                    serviceProvider => ActivatorUtilities.CreateInstance<ProjectEntityMessageHandler>(serviceProvider)));
            });

            return builder;
        }

        private static Action<IProjectionBuilder> BuildProjectionConfiguration(Action<IProjectionBuilder>? configuration)
        {
            if (configuration != null)
            {
                return builder =>
                {
                    builder.UseSourceProcessor<EntityStorageEngineProjectionSourceProcessorFactory>();
                    configuration(builder);
                };
            }
            else
            {
                return builder => builder.UseSourceProcessor<EntityStorageEngineProjectionSourceProcessorFactory>();
            }
        }
    }
}
