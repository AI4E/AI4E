using System;
using System.Linq;
using AI4E.Storage.Projection;
using AI4E.Utils;
using AI4E.Utils.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AI4E.Storage
{
    public static class ProjectionStorageBuilderExtension
    {
        public static IStorageBuilder AddProjection(this IStorageBuilder storageBuilder)
        {
            var services = storageBuilder.Services;

            services.TryAddSingleton<IProjectionRegistry, ProjectionRegistry>();
            services.TryAddSingleton<IProjectionEngine, ProjectionEngine>();
            services.ConfigureApplicationParts(ProjectionFeatureProvider.Configure);
            Projections.Register(storageBuilder);

            return storageBuilder;
        }

        public static IStorageBuilder ConfigureProjections(
            this IStorageBuilder storageBuilder,
            Action<IProjectionRegistry, IServiceProvider> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            storageBuilder.Services.Decorate<IProjectionRegistry>((registry, provider) =>
            {
                configuration(registry, provider);
                return registry;
            });

            return storageBuilder;
        }
    }
}
