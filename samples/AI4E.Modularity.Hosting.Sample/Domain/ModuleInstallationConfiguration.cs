using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleInstallationConfiguration : AggregateRoot<SingletonId> // TODO: Rename
    {
        private readonly Dictionary<ModuleIdentifier, ModuleVersion> _installedModules;
        private readonly Dictionary<ModuleIdentifier, ModuleVersion> _resolvedModules;

        public ModuleInstallationConfiguration() : base(id: default)
        {
            _installedModules = new Dictionary<ModuleIdentifier, ModuleVersion>();
            _resolvedModules = new Dictionary<ModuleIdentifier, ModuleVersion>();
        }

        public Task ModuleInstalledAsync(ModuleIdentifier module,
                                         ModuleVersion version,
                                         IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                         CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (_installedModules.ContainsKey(module))
                throw new InvalidOperationException("The specified module is already installed.");

            _installedModules.Add(module, version);

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public Task ModuleUpdatedAsync(ModuleIdentifier module,
                                       ModuleVersion version,
                                       IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                       CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (!_installedModules.ContainsKey(module))
                throw new InvalidOperationException("The specified module is not installed.");

            _installedModules[module] = version;

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public Task ModuleUninstalledAsync(ModuleIdentifier module,
                                           IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                           CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (!_installedModules.Remove(module))
                throw new InvalidOperationException("The specified module is not installed.");

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public IEnumerable<ModuleReleaseIdentifier> InstalledModules => _installedModules.Select(p => new ModuleReleaseIdentifier(p.Key, p.Value));

        public Task ReleaseAddedAsync(ModuleIdentifier module,
                                      ModuleVersion version,
                                      IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                      CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            // TODO: Is this ok?
            if (!_resolvedModules.ContainsKey(module))
            {
                return Task.CompletedTask;
            }

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        public Task ReleaseRemovedAsync(ModuleIdentifier module,
                                        ModuleVersion version,
                                        IDependencyResolver dependencyResolver, // TODO: This should be injected via DI
                                        CancellationToken cancellation = default)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            // TODO: Is this ok?
            if (!_resolvedModules.ContainsKey(module))
            {
                return Task.CompletedTask;
            }

            return ResolveDependenciesAsync(dependencyResolver, cancellation);
        }

        private async Task ResolveDependenciesAsync(IDependencyResolver dependencyResolver,
                                                    CancellationToken cancellation)
        {
            var unresolvedDependencies = _installedModules.Select(p => new ModuleDependency(p.Key, ModuleVersionRange.SingleVersion(p.Value)));
            var unresolvedInstallationSet = new UnresolvedInstallationSet(resolved: Enumerable.Empty<ModuleReleaseIdentifier>(),
                                                                          unresolved: unresolvedDependencies);

            var resolvedInstallationSets = (await unresolvedInstallationSet.ResolveAsync(dependencyResolver, cancellation)).ToList();

            if (resolvedInstallationSets.Count() == 0)
            {
                return; // TODO: What can we do here?
            }

            resolvedInstallationSets.Sort();

            var installationSet = resolvedInstallationSets.First();

            _resolvedModules.Clear();

            foreach (var resolvedRelease in installationSet.ResolvedReleases)
            {
                _resolvedModules.Add(resolvedRelease.Module, resolvedRelease.Version);
            }
        }
    }

    public readonly struct SingletonId { }
}
