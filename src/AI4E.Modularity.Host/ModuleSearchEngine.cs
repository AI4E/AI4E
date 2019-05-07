using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Storage.Domain;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleSearchEngine : IModuleSearchEngine
    {
        private readonly IMetadataReader _metadataReader;
        private readonly IEntityStorageEngine _entityStorageEngine;

        public ModuleSearchEngine(IMetadataReader metadataReader,
                                  IEntityStorageEngine entityStorageEngine)
        {
            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            if (entityStorageEngine == null)
                throw new ArgumentNullException(nameof(entityStorageEngine));

            _metadataReader = metadataReader;
            _entityStorageEngine = entityStorageEngine;
        }

        async Task<IEnumerable<IModule>> IModuleSearchEngine.SearchModulesAsync(string searchPhrase,
                                                                                bool includePreReleases,
                                                                                CancellationToken cancellation)
        {
            return await SearchModulesAsync(searchPhrase, includePreReleases, cancellation);
        }

        public async Task<IEnumerable<Module>> SearchModulesAsync(string searchPhrase,
                                                                  bool includePreReleases,
                                                                  CancellationToken cancellation)
        {
            var sources = await _entityStorageEngine.GetAllAsync<IModuleSource>(cancellation).ToArrayAsync();
            var result = new Dictionary<ModuleIdentifier, Module>();

            foreach (var source in sources)
            {
                await ProcessSourceAsync(source, searchPhrase, includePreReleases, result, cancellation);
            }

            // We store the sources for the case that internal state changed (like caches), but ignore any concurrency conflicts.
            await Task.WhenAll(sources.Select(source => _entityStorageEngine.TryStoreAsync(source, cancellation)));

            return result.Values;
        }

        private async Task ProcessSourceAsync(IModuleSource source,
                                              string searchPhrase,
                                              bool includePreReleases,
                                              IDictionary<ModuleIdentifier, Module> resultSet,
                                              CancellationToken cancellation)
        {
            var available = await source.GetAvailableAsync(searchPhrase, includePreReleases, _metadataReader, cancellation);

            foreach (var releaseId in available)
            {
                var module = await GetModuleAsync(source, releaseId, cancellation);

#if DEBUG
                if (resultSet.TryGetValue(module.Id, out var existingModule))
                {
                    Assert(existingModule.Revision <= module.Revision);
                }
#endif

                // We override any modules that were stored previously. 
                // We are allowed to do this, as GetModuleAsync is guaranteed to return the same or a later version of the module entity.
                resultSet[module.Id] = module;
            }
        }

        private async ValueTask<Module> GetModuleAsync(IModuleSource source,
                                                       ModuleReleaseIdentifier releaseId,
                                                       CancellationToken cancellation)
        {
            var moduleId = releaseId.Module;
            var moduleVersion = releaseId.Version;

            Module module;

            do
            {
                module = await _entityStorageEngine.GetByIdAsync<Module>(moduleId.ToString(), cancellation);

                if (module == null)
                {
                    var metadata = await source.GetMetadataAsync(releaseId, _metadataReader, cancellation);
                    module = new Module(metadata, source);
                }
                else if (module.GetRelease(moduleVersion) is var release && release != null)
                {
                    if (release.TryAddSource(source))
                    {
                        break;
                    }
                }
                else
                {
                    var metadata = await source.GetMetadataAsync(releaseId, _metadataReader, cancellation);
                    module.AddRelease(metadata, source);
                }
            }
            while (!await _entityStorageEngine.TryStoreAsync(module, cancellation));

            return module;
        }
    }
}
