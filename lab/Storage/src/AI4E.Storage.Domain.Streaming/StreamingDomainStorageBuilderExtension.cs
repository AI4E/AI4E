using System;
using AI4E.Messaging;
using AI4E.Storage.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace AI4E.Storage.Domain.Streaming
{
    public static partial class StreamingDomainStorageBuilderExtension
    {
        public static IDomainStorageBuilder UseStreaming(this IDomainStorageBuilder builder)
        {
#pragma warning disable CA1062
            var services = builder.Services;
#pragma warning restore CA1062

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

            return builder;
        }

        public static IDomainStorageBuilder UseStreaming(
            this IDomainStorageBuilder builder,
            Action<StreamingOptions> configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var result = UseStreaming(builder);
#pragma warning disable CA1062
            result.Services.Configure(configuration);
#pragma warning restore CA1062
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
    }
}
