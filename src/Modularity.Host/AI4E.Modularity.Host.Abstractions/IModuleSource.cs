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

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;

namespace AI4E.Modularity.Host
{
    // TODO: The IMetadataReader is a dependency of the implementation actually.
    //       Currently there is no support to use dependency injection in the domain model.
    //       If this support is added, the parameters can be removed.
    public interface IModuleSource
    {
        #region Workaround // TODO

        // This is needed currently in order for the domain storage engine to read the properties 
        // from the respective entities as the domain storage engine uses the statically known type for property access.

        System.Guid Id { get; }
        long Revision { get; set; }
        string ConcurrencyToken { get; set; }

        #endregion

        ModuleSourceName Name { get; set; }

        Task<IEnumerable<ModuleReleaseIdentifier>> GetAvailableAsync(string searchPhrase,
                                                                     bool includePreReleases,
                                                                     IMetadataReader moduleMetadataReader,
                                                                     CancellationToken cancellation = default);

        ValueTask<IModuleMetadata> GetMetadataAsync(ModuleIdentifier module,
                                                    ModuleVersion version,
                                                    IMetadataReader moduleMetadataReader,
                                                    CancellationToken cancellation = default);

        ValueTask<DirectoryInfo> ExtractAsync(DirectoryInfo directory,
                                              ModuleReleaseIdentifier module,
                                              IMetadataReader moduleMetadataReader,
                                              CancellationToken cancellation = default);
    }
}
