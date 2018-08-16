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

            using (stream)
            {
                var readableStream = await GetReadableStreamAsync(stream, cancellation);

                return ReadMetadataInternal(readableStream);
            }
        }

        private async ValueTask<MemoryStream> GetReadableStreamAsync(Stream stream, CancellationToken cancellation)
        {
            if (stream is MemoryStream result)
            {
                return result;
            }

            if (stream.CanSeek)
            {
                if (stream.Length > int.MaxValue)
                    throw new InvalidOperationException("Unable to read module metadata. The streams size exceeds the readable limit.");

                result = new MemoryStream(checked((int)stream.Length));
            }
            else
            {
                result = new MemoryStream();
            }

            await stream.CopyToAsync(result, bufferSize: 1024, cancellation);
            result.Position = 0;
            return result;
        }

        private IModuleMetadata ReadMetadataInternal(MemoryStream stream)
        {
            var serializer = JsonSerializer.CreateDefault();

            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {
                // TODO: Validate metadata

                try
                {
                    return serializer.Deserialize<SerializedModuleMetadata>(reader);
                }
                catch (JsonSerializationException exc)
                {
                    throw new ModuleMetadataFormatException("The module metadata is malformed.", exc);
                }
            }
        }

        private sealed class SerializedModuleMetadata : IModuleMetadata
        {
            [JsonProperty("module")]
            public ModuleIdentifier Module { get; set; }

            [JsonProperty("version")]
            public ModuleVersion Version { get; set; }

            ModuleReleaseIdentifier IModuleMetadata.Release => new ModuleReleaseIdentifier(Module, Version);

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

            IEnumerable<ModuleDependency> IModuleMetadata.Dependencies => Dependencies.Select(p => new ModuleDependency(p.Key, p.Value));

            [JsonProperty("dependencies")]
            public Dictionary<ModuleIdentifier, ModuleVersionRange> Dependencies { get; } = new Dictionary<ModuleIdentifier, ModuleVersionRange>();
        }
    }
}
