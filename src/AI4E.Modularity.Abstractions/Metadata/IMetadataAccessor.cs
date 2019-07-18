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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Metadata
{
    public interface IMetadataAccessor
    {
        ValueTask<IModuleMetadata> GetMetadataAsync(Assembly entryAssembly, CancellationToken cancellation = default);
    }

    public static class MetadataAccessorExtension
    {
        public static ValueTask<IModuleMetadata> GetMetadataAsync(this IMetadataAccessor metadataAccessor, CancellationToken cancellation = default)
        {
            if (metadataAccessor == null)
                throw new ArgumentNullException(nameof(metadataAccessor));

            var entryAssembly = Assembly.GetEntryAssembly();
            return metadataAccessor.GetMetadataAsync(entryAssembly, cancellation);
        }
    }
}
