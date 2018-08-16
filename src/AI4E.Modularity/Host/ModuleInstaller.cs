using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            using (var scope = _serviceProvider.CreateScope())
            {
                var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>();
                return await storageEngine.GetByIdAsync<Module>(module.ToString(), cancellation);
            }
        }
    }
}
