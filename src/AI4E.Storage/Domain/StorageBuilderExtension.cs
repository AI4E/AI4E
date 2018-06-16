using System;
using System.Linq;
using AI4E.Storage.Projection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;

namespace AI4E.Storage.Domain
{
    public static class StorageBuilderExtension
    {
        public static IDomainStorageBuilder UseDomainStorage(this IStorageBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var services = builder.Services;

            // Configure necessary application parts
            ConfigureApplicationParts(services);

            services.AddTransient<IStreamStore, StreamStore>();
            services.AddSingleton(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            });

            services.AddSingleton<ICommitDispatcher, EntityStorageEngine.CommitDispatcher>();
            services.AddSingleton<ISnapshotProcessor, EntityStorageEngine.SnapshotProcessor>();
            services.AddSingleton<IEntityAccessor, DefaultEntityAccessor>();
            services.AddTransient(provider => Provider.Create<EntityStorageEngine>(provider));
            services.AddScoped<IEntityStorageEngine>(
                provider => provider.GetRequiredService<IProvider<EntityStorageEngine>>()
                                    .ProvideInstance());

            services.AddSingleton<IStreamPersistence, StreamPersistence>();
            services.AddSingleton(BuildProjector);

            services.Configure<MessagingOptions>(options =>
            {
                options.MessageProcessors.Add(ContextualProvider.Create<EntityMessageHandlerProcessor>());
            });

            var result = new DomainStorageBuilder(builder);
            return result;
        }

        public static IDomainStorageBuilder UseDomainStorage(this IStorageBuilder builder, Action<DomainStorageOptions> configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = UseDomainStorage(builder);
            result.Services.Configure(configuration);
            return result;
        }

        private static IProjector BuildProjector(IServiceProvider serviceProvider)
        {
            var projector = new Projector(serviceProvider);

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

        private static void ConfigureApplicationParts(IServiceCollection services)
        {
            var partManager = services.GetApplicationPartManager();
            partManager.ConfigureMessagingFeatureProviders();
            services.TryAddSingleton(partManager);
        }

        private static void ConfigureMessagingFeatureProviders(this ApplicationPartManager partManager)
        {
            if (!partManager.FeatureProviders.OfType<ProjectionFeatureProvider>().Any())
            {
                partManager.FeatureProviders.Add(new ProjectionFeatureProvider());
            }
        }

        private static ApplicationPartManager GetApplicationPartManager(this IServiceCollection services)
        {
            var manager = services.GetService<ApplicationPartManager>();
            if (manager == null)
            {
                manager = new ApplicationPartManager();
            }

            return manager;
        }

        private static T GetService<T>(this IServiceCollection services)
        {
            var serviceDescriptor = services.LastOrDefault(d => d.ServiceType == typeof(T));

            return (T)serviceDescriptor?.ImplementationInstance;
        }
    }
}
