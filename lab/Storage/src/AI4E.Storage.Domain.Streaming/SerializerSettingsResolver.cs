using Newtonsoft.Json;

namespace AI4E.Storage.Domain.Streaming
{
    internal sealed class SerializerSettingsResolver : ISerializerSettingsResolver
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
