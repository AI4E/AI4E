using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AI4E.Modularity
{
    public sealed class MetadataWriter : IMetadataWriter
    {
        public async Task WriteMetadataAsync(Stream stream, IModuleMetadata metadata, CancellationToken cancellation)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            using (var memoryStream = new MemoryStream())
            {
                WriteMetadataInternal(memoryStream, metadata);

                memoryStream.Position = 0;

                using (stream)
                {
                    await memoryStream.CopyToAsync(stream, bufferSize: 4096, cancellation);
                }
            }
        }

        private void WriteMetadataInternal(MemoryStream stream, IModuleMetadata metadata)
        {
            var serializer = JsonSerializer.CreateDefault();

            using (var streamWriter = new StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter))
            {
                try
                {
                    var serializedMetadata = (metadata as SerializedModuleMetadata) ?? new SerializedModuleMetadata(metadata);
                    serializer.Serialize(writer, serializedMetadata, typeof(SerializedModuleMetadata));
                }
                catch (JsonSerializationException exc)
                {
                    throw new ModuleMetadataFormatException("The module metadata is malformed.", exc);
                }
            }
        }
    }
}
