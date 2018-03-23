using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AI4E.Modularity
{
    public sealed class MetadataReader : IMetadataReader
    {
        public async Task<IModuleMetadata> ReadMetadataAsync(Stream stream, CancellationToken cancellation)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!(stream is MemoryStream memStream))
            {
                if (stream.CanSeek)
                {
                    memStream = new MemoryStream(checked((int)stream.Length));
                }
                else
                {
                    memStream = new MemoryStream();
                }

                await stream.CopyToAsync(memStream, 1024, cancellation);
                memStream.Position = 0;
            }

            // Disposing a memory stream is a no-op
            using (stream)
            {
                var serializer = JsonSerializer.CreateDefault();

                using (var streamReader = new StreamReader(memStream))
                using (var reader = new JsonTextReader(streamReader))
                {
                    return serializer.Deserialize<SerializedModuleMetadata>(reader);
                }
            }
        }

        private sealed class SerializedModuleMetadata : IModuleMetadata
        {
            [JsonProperty("id")]
            public ModuleIdentifier Id { get; set; }

            [JsonProperty("version")]
            public ModuleVersion Version { get; set; }

            [JsonProperty("release-date")]
            public DateTime ReleaseDate { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("author")]
            public string Author { get; set; }

            [JsonProperty("entry-command")]
            public string EntryAssemblyCommand { get; set; }

            [JsonProperty("entry-arguments")]
            public string EntryAssemblyArguments { get; set; }

            IEnumerable<IModuleDependency> IModuleMetadata.Dependencies => Dependencies.Select(p => new SerializedModuleDependency { Id = p.Key, Version = p.Value });

            [JsonProperty("dependencies")]
            public IDictionary<ModuleIdentifier, ModuleVersionFilter> Dependencies { get; } = new Dictionary<ModuleIdentifier, ModuleVersionFilter>();
        }

        [JsonDictionary("dependencies", Id = "Id")]
        private sealed class SerializedModuleDependency : IModuleDependency
        {
            public ModuleIdentifier Id { get; set; }

            public ModuleVersionFilter Version { get; set; }
        }
    }
}
