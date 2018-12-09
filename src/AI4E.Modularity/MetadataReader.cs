using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
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
                var readableStream = await stream.ReadToMemoryAsync(cancellation);

                return ReadMetadataInternal(readableStream);
            }
        }

        private IModuleMetadata ReadMetadataInternal(MemoryStream stream)
        {
            var serializer = JsonSerializer.CreateDefault();

            using (var streamReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(streamReader))
            {


                try
                {
                    var metadata = serializer.Deserialize<SerializedModuleMetadata>(reader);

                    // Validate metadata
                    if (metadata.Module == default || metadata.Version == default)
                    {
                        throw new ModuleMetadataFormatException("The module metadata is malformed.");
                    }

                    return metadata;
                }
                catch (JsonSerializationException exc)
                {
                    throw new ModuleMetadataFormatException("The module metadata is malformed.", exc);
                }
            }
        }
    }
}
