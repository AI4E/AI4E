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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;
using AI4E.Storage.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleInstaller : IModuleInstaller
    {
        private readonly IMetadataReader _metadataReader;
        private readonly IServiceProvider _serviceProvider;

        public ModuleInstaller(IMetadataReader metadataReader, IServiceProvider serviceProvider)
        {
            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _metadataReader = metadataReader;
            _serviceProvider = serviceProvider;
        }

        public async Task<DirectoryInfo> InstallAsync(DirectoryInfo directory,
                                                      ModuleReleaseIdentifier moduleRelease,
                                                      CancellationToken cancellation)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            var sources = await GetSourcesAsync(moduleRelease, cancellation);

            foreach (var source in sources)
            {
                var result = await source.ExtractAsync(directory, moduleRelease, _metadataReader, cancellation);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private async Task<IEnumerable<IModuleSource>> GetSourcesAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var module = await GetModuleAsync(moduleRelease.Module, cancellation);

            if (module == null)
            {
                return Enumerable.Empty<IModuleSource>();
            }

            var release = module.GetRelease(moduleRelease.Version);

            if (release == null)
            {
                return Enumerable.Empty<IModuleSource>();
            }

            return await release.GetSourcesAsync(cancellation);
        }

        // TODO: Refactor to own component
        private async ValueTask<Module> GetModuleAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            using var scope = _serviceProvider.CreateScope();
            var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>();
            return await storageEngine.GetByIdAsync<Module>(module.ToString(), cancellation);
        }
    }
}
