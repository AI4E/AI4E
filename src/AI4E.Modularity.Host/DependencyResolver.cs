using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;
using AI4E.Storage.Domain;

namespace AI4E.Modularity.Host
{
    public sealed class DependencyResolver : IDependencyResolver
    {
        private readonly IEntityStorageEngine _storageEngine;

        public DependencyResolver(IEntityStorageEngine storageEngine)
        {
            if (storageEngine == null)
                throw new ArgumentNullException(nameof(storageEngine));

            _storageEngine = storageEngine;
        }

        public async ValueTask<IEnumerable<ModuleReleaseIdentifier>> GetMatchingReleasesAsync(ModuleDependency dependency, CancellationToken cancellation)
        {
            var module = await _storageEngine.GetByIdAsync(typeof(Module), dependency.Module.ToString(), cancellation) as Module;

            if (module == null)
            {
                return Enumerable.Empty<ModuleReleaseIdentifier>(); // TODO: Is this correct?
            }

            return module.GetMatchingReleases(dependency.VersionRange).Select(p => p.Id);
        }

        public async ValueTask<IEnumerable<ModuleDependency>> GetDependenciesAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var module = await _storageEngine.GetByIdAsync(typeof(Module), moduleRelease.Module.ToString(), cancellation) as Module;

            if (module == null)
            {
                throw new Exception(); // TODO
            }

            var release = module.GetRelease(moduleRelease.Version);

            if (release == null)
            {
                throw new Exception(); // TODO
            }

            return release.Dependencies;
        }
    }
}
