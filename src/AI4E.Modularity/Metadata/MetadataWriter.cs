/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AI4E.Modularity.Metadata
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

            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
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
