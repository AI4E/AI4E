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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using Newtonsoft.Json;

namespace AI4E.Modularity.Metadata
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
