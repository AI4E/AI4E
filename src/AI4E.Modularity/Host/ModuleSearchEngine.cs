﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Storage.Domain;

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
            var sources = await _entityStorageEngine.GetAllAsync(typeof(FileSystemModuleSource), cancellation).Cast<FileSystemModuleSource>().ToArray();
            var result = new HashSet<Module>();

            foreach (var source in sources)
            {
                await ProcessSourceAsync(source, searchPhrase, includePreReleases, result, cancellation);
            }

            // We store the sources for the case that internal state changed (like caches), but ignore any concurrency conflicts.
            await Task.WhenAll(sources.Select(source => _entityStorageEngine.TryStoreAsync(source, cancellation)));

            return result;
        }

        private async Task ProcessSourceAsync(FileSystemModuleSource source,
                                              string searchPhrase,
                                              bool includePreReleases,
                                              HashSet<Module> resultSet,
                                              CancellationToken cancellation)
        {
            var available = await source.GetAvailableAsync(searchPhrase, includePreReleases, _metadataReader, cancellation);

            foreach (var releaseId in available)
            {
                var module = await GetModuleAsync(source, releaseId, cancellation);

                resultSet.Add(module);
            }
        }

        private async ValueTask<Module> GetModuleAsync(FileSystemModuleSource source,
                                                       ModuleReleaseIdentifier releaseId,
                                                       CancellationToken cancellation)
        {
            var moduleId = releaseId.Module;
            var moduleVersion = releaseId.Version;

            Module module;

            do
            {
                module = (await _entityStorageEngine.GetByIdAsync(typeof(Module), moduleId.ToString(), cancellation)) as Module;

                if (module == null)
                {
                    var metadata = await source.GetMetadataAsync(releaseId, _metadataReader, cancellation);
                    module = new Module(metadata, source);
                }
                else if (module.GetRelease(moduleVersion) is var release && release != null)
                {
                    release.AddSource(source);
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
