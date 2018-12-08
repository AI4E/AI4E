using System;
using System.Linq;
using AI4E.ApplicationParts;
using AI4E.ApplicationParts.Utils;
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;
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
            services.ConfigureApplicationParts(ConfigureFeatureProviders);

            // TODO: Replace
            //services.AddSingleton(new JsonSerializerSettings
            //{
            //    TypeNameHandling = TypeNameHandling.Auto,
            //    Formatting = Formatting.Indented,
            //    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            //    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            //    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            //});

            services.AddTransient<ISerializerSettingsResolver, SerializerSettingsResolver>();

            AddStreamStore(services);
            AddDomainStorageEngine(services);
            AddProjectionEngine(services);
            AddMessageProcessors(services);

            return new DomainStorageBuilder(builder);
        }

        public static IDomainStorageBuilder UseDomainStorage(this IStorageBuilder builder,
                                                             Action<DomainStorageOptions> configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = UseDomainStorage(builder);
            result.Services.Configure(configuration);
            return result;
        }

        private static void AddStreamStore(IServiceCollection services)
        {
            services.AddSingleton<IStreamPersistence, StreamPersistence>();
            services.AddScoped<IStreamStore, StreamStore>();
        }

        private static void AddDomainStorageEngine(IServiceCollection services)
        {
            // Domain storage engine
            services.AddScoped<IEntityStorageEngine, EntityStorageEngine>();

            // Domain storage engine background worker
            services.AddSingleton<ICommitDispatcher, CommitDispatcher>();
            services.AddSingleton<ISnapshotProcessor, SnapshotProcessor>();

            // Helpers
            services.AddSingleton<IEntityPropertyAccessor, EntityPropertyAccessor>();

            //services.AddSingleton(typeof(IEntityIdAccessor<,>), typeof(DefaultEntityIdAccessor<,>));
        }

        private static void AddProjectionEngine(IServiceCollection services)
        {
            services.AddSingleton(BuildProjector);
            services.AddSingleton<IProjectionEngine, ProjectionEngine>();
            services.AddScoped<IProjectionSourceLoader, ProjectionSourceLoader>();
        }

        private static void AddMessageProcessors(IServiceCollection services)
        {
            services.Configure<MessagingOptions>(options =>
            {
                options.MessageProcessors.Add(ContextualProvider.Create<EntityMessageHandlerProcessor>());
            });
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

        private sealed class SerializerSettingsResolver : ISerializerSettingsResolver
        {
            public JsonSerializerSettings ResolveSettings(IEntityStorageEngine entityStorageEngine)
            {
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    Formatting = Formatting.Indented,
                    PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                    ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
                };

                return settings;
            }
        }
    }
}
