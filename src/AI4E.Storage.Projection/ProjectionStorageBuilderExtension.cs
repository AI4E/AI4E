using System;
using System.Linq;
using AI4E.Storage.Projection;
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

            services.TryAddSingleton(typeof(IContextualProvider<>), typeof(ContextualProvider<>));

            // Configure necessary application parts
            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            services.AddSingleton(BuildProjector);
            services.AddSingleton<IProjectionEngine, ProjectionEngine>();

            return storageBuilder;
        }

        private static IProjector BuildProjector(IServiceProvider serviceProvider)
        {
            var projector = new Projector();
            var partManager = serviceProvider.GetRequiredService<ApplicationPartManager>();
            var projectionFeature = new ProjectionFeature();

            partManager.PopulateFeature(projectionFeature);

            foreach (var type in projectionFeature.Projections)
            {
                var inspector = new ProjectionInspector(type);
                var descriptors = inspector.GetDescriptors();

                foreach (var descriptor in descriptors)
                {
                    var provider = Activator.CreateInstance(typeof(ProjectionInvoker<,>.Provider).MakeGenericType(descriptor.SourceType, descriptor.ProjectionType),
                                                            type,
                                                            descriptor);

                    var registerMethodDefinition = typeof(IProjector).GetMethods().Single(p => p.Name == "RegisterProjection" && p.IsGenericMethodDefinition && p.GetGenericArguments().Length == 2);
                    var registerMethod = registerMethodDefinition.MakeGenericMethod(descriptor.SourceType, descriptor.ProjectionType);
                    registerMethod.Invoke(projector, new object[] { provider });
                }
            }

            return projector;
        }

        private static void ConfigureFeatureProviders(ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<ProjectionFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new ProjectionFeatureProvider());
            }
        }
    }
}
