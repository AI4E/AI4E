using System;
using AI4E.Storage.Domain.Projection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Storage.Domain
{
    public static class ProjectionDomainStorageBuilderExtension
    {
        public static IDomainStorageBuilder AddProjection(this IDomainStorageBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            AddProjectionCore(builder);
            return builder;
        }

        public static IDomainStorageBuilder AddProjection(
            this IDomainStorageBuilder builder,
            Action<IProjectionBuilder> configuration)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            var projectionBuilder = AddProjectionCore(builder);
            configuration(projectionBuilder);

            return builder;
        }

        private static IProjectionBuilder AddProjectionCore(IDomainStorageBuilder builder)
        {
            var projectionBuilder = builder.Services.GetService<ProjectionBuilder>();

            if (projectionBuilder is null)
            {
                projectionBuilder = new ProjectionBuilder(builder);

                projectionBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(projectionBuilder);
                    services.TryAddSingleton<IProjectionRegistry, ProjectionRegistry>();
                    services.TryAddSingleton<IProjectionEngine, ProjectionEngine>();
                    services.TryAddSingleton<IProjectionTargetProcessorFactory, ProjectionTargetProcessorFactory>();
                    services.TryAddSingleton<IProjectionExecutor, ProjectionExecutor>();
                    services.TryAddSingleton<IProjectionSourceProcessorFactory, EntityStorageEngineProjectionSourceProcessorFactory>();
                    services.ConfigureApplicationParts(ProjectionFeatureProvider.Configure);
                });

                Projections.Register(projectionBuilder);
                ProjectionCommitAttemptProcessor.Register(projectionBuilder);
                ProjectEntityMessageHandler.Register(projectionBuilder);
            }

            return projectionBuilder;
        }

        private sealed class ProjectionBuilder : IProjectionBuilder
        {
            public ProjectionBuilder(IDomainStorageBuilder domainStorageBuilder)
            {
                DomainStorageBuilder = domainStorageBuilder;
            }

            public IDomainStorageBuilder DomainStorageBuilder { get; }

            public IProjectionBuilder ConfigureServices(Action<IServiceCollection> configuration)
            {
                configuration(DomainStorageBuilder.Services);

                return this;
            }
        }
    }
}
