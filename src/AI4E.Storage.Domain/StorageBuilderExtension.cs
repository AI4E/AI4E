using System;
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

            // TODO: Replace
            services.AddSingleton(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
            });

            services.AddTransient<ISerializerSettingsResolver, SerializerSettingsResolver>();

            AddStreamStore(services);
            AddDomainStorageEngine(services);
            AddMessageProcessors(services);

            services.AddSingleton<IProjectionSourceProcessorFactory, ProjectionSourceProcessorFactory>();
            builder.AddProjection();

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
            services.AddSingleton<IStreamStore, StreamStore>();
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
        }

        private static void AddMessageProcessors(IServiceCollection services)
        {
            services.Configure<MessagingOptions>(options =>
            {
                options.MessageProcessors.Add(MessageProcessorRegistration.Create<EntityMessageHandlerProcessor>());
            });
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
