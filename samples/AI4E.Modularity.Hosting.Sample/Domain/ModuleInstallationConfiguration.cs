using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Internal;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleInstallationConfiguration : AggregateRoot<SingletonId> // TODO: Rename
    {
        // TODO: This is not serializable.
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

    public readonly struct UnresolvedInstallationSet
    {
        private readonly ImmutableDictionary<ModuleIdentifier, ModuleVersion> _resolved;
        private readonly ImmutableDictionary<ModuleIdentifier, ModuleVersionRange> _unresolved;

        public UnresolvedInstallationSet(IEnumerable<ModuleReleaseIdentifier> resolved,
                                         IEnumerable<ModuleDependency> unresolved)
        {
            if (resolved == null)
                throw new ArgumentNullException(nameof(resolved));

            if (unresolved == null)
                throw new ArgumentNullException(nameof(unresolved));

            _resolved = resolved.ToImmutableDictionary(p => p.Module, p => p.Version);
            _unresolved = unresolved.ToImmutableDictionary(p => p.Module, p => p.VersionRange);
        }

        public UnresolvedInstallationSet(ImmutableDictionary<ModuleIdentifier, ModuleVersion> resolved,
                                         ImmutableDictionary<ModuleIdentifier, ModuleVersionRange> unresolved)
        {
            if (resolved == null)
                throw new ArgumentNullException(nameof(resolved));

            if (unresolved == null)
                throw new ArgumentNullException(nameof(unresolved));

            _resolved = resolved;
            _unresolved = unresolved;
        }

        public IEnumerable<ModuleReleaseIdentifier> ResolvedReleases => _resolved?.Select(p => new ModuleReleaseIdentifier(p.Key, p.Value))
                                                                                 ?? Enumerable.Empty<ModuleReleaseIdentifier>();
        public IEnumerable<ModuleDependency> Unresolved => _unresolved?.Select(p => new ModuleDependency(p.Key, p.Value))
                                                                      ?? Enumerable.Empty<ModuleDependency>();

        public async Task<IEnumerable<ResolvedInstallationSet>> ResolveAsync(IDependencyResolver dependencyResolver, CancellationToken cancellation)
        {
            if (dependencyResolver == null)
                throw new ArgumentNullException(nameof(dependencyResolver));

            var result = new List<ResolvedInstallationSet>();

            if (TryGetUnresolved(out var dependency))
            {
                Assert(_unresolved != null);

                // A conflict occured
                return await TryResolveSingleDependencyAsync(dependency, dependencyResolver, cancellation);
            }

            return new ResolvedInstallationSet(ResolvedReleases).Yield();
        }

        private bool TryGetUnresolved(out ModuleDependency dependency)
        {
            if (_unresolved == null)
            {
                dependency = default;
                return false;
            }

            var module = _unresolved.Keys.FirstOrDefault();

            if (module == null)
            {
                dependency = default;
                return false;
            }

            var sucess = _unresolved.TryGetValue(module, out var version);
            Assert(sucess);

            dependency = new ModuleDependency(module, version);
            return true;
        }

        private async Task<IEnumerable<ResolvedInstallationSet>> TryResolveSingleDependencyAsync(ModuleDependency dependency,
                                                                                                 IDependencyResolver dependencyResolver,
                                                                                                 CancellationToken cancellation)
        {
            // We have a resolved dependency that does not match our version filter => This is a version conflict
            if (_resolved != null &&
                _resolved.TryGetValue(dependency.Module, out var resolvedDependency) &&
               !dependency.VersionRange.IsMatch(resolvedDependency))
            {
                return Enumerable.Empty<ResolvedInstallationSet>();
            }

            var matchingReleases = await dependencyResolver.GetMatchingReleasesAsync(dependency, cancellation);

            var resolvedInstallationSets = new List<ResolvedInstallationSet>();
            var resolveTasks = new List<Task<IEnumerable<ResolvedInstallationSet>>>();

            foreach (var matchingRelease in matchingReleases)
            {
                var dependencies = await dependencyResolver.GetDependenciesAsync(matchingRelease, cancellation);

                if (!TryCombine(matchingRelease, dependencies, out var unresolved))
                {
                    continue;
                }

                var resolved = (_resolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersion>.Empty).Add(matchingRelease.Module, matchingRelease.Version);

                // If there are no unresolved dependencies for the release, we can short circuit.
                if (unresolved.Count == 0)
                {
                    resolvedInstallationSets.Add(new ResolvedInstallationSet(resolved.Select(p => new ModuleReleaseIdentifier(p.Key, p.Value))));
                }
                else
                {
                    var unresolvedInstallationSet = new UnresolvedInstallationSet(resolved, unresolved);

                    resolveTasks.Add(unresolvedInstallationSet.ResolveAsync(dependencyResolver, cancellation));
                }
            }

            // TODO: Is it possible that there are duplicates?
            var result = (await Task.WhenAll(resolveTasks)).SelectMany(_ => _);

            return result;
        }

        private bool TryCombine(ModuleReleaseIdentifier matchingRelease,
                                IEnumerable<ModuleDependency> dependencies,
                                out ImmutableDictionary<ModuleIdentifier, ModuleVersionRange> unresolved)
        {
            Assert(_unresolved != null);
            var builder = _unresolved.ToBuilder();
            builder.Remove(matchingRelease.Module);

            foreach (var dependency in dependencies)
            {
                if (_resolved != null &&
                    _resolved.TryGetValue(dependency.Module, out var resolvedDependency))
                {
                    if (dependency.VersionRange.IsMatch(resolvedDependency))
                    {
                        continue;
                    }

                    unresolved = null;
                    return false; // We have a resolved dependency that does not match our version filter => This is a version conflict
                }

                if (matchingRelease.Module == dependency.Module)
                {
                    if (dependency.VersionRange.IsMatch(matchingRelease.Version))
                    {
                        continue;
                    }

                    unresolved = null;
                    return false;
                }

                if (!builder.TryGetValue(dependency.Module, out var existingVersion))
                {
                    builder.Add(dependency.Module, dependency.VersionRange);
                }
                else if (existingVersion.TryCombine(dependency.VersionRange, out var combinedVersion))
                {
                    builder[dependency.Module] = combinedVersion;
                }
                else
                {
                    unresolved = null;
                    return false; // We have an unresolved dependency that does not match our version filter => This is a version conflict
                }
            }

            unresolved = builder.ToImmutable();
            return true;
        }
    }

    public readonly struct ResolvedInstallationSet : IComparable<ResolvedInstallationSet>
    {
        private readonly ImmutableDictionary<ModuleIdentifier, ModuleVersion> _resolved;

        public ResolvedInstallationSet(IEnumerable<ModuleReleaseIdentifier> resolved)
        {
            if (resolved == null)
                throw new ArgumentNullException(nameof(resolved));

            _resolved = resolved.ToImmutableDictionary(p => p.Module, p => p.Version);
        }

        public ResolvedInstallationSet(ImmutableDictionary<ModuleIdentifier, ModuleVersion> resolved)
        {
            if (resolved == null)
                throw new ArgumentNullException(nameof(resolved));

            _resolved = resolved;
        }

        public IEnumerable<ModuleReleaseIdentifier> ResolvedReleases => _resolved?.Select(p => new ModuleReleaseIdentifier(p.Key, p.Value))
                                                                                 ?? Enumerable.Empty<ModuleReleaseIdentifier>();

        public bool ContainsModule(ModuleIdentifier module)
        {
            if (_resolved == null)
                return false;

            return _resolved.ContainsKey(module);
        }

        public ModuleVersion? GetResolvedVersion(ModuleIdentifier module)
        {
            if (_resolved == null)
                return null;

            if (!_resolved.TryGetValue(module, out var result))
            {
                return null;
            }

            return result;
        }

        public int CompareTo(ResolvedInstallationSet other)
        {
            var keys = _resolved.Keys.Intersect(other._resolved.Keys);

            var result = 0;

            foreach (var key in keys)
            {
                var version = _resolved[key];
                var otherVersion = other._resolved[key];

                result -= Math.Sign(version.CompareTo(otherVersion));
            }

            if (result != 0)
                return result;

            return _resolved.Count - other._resolved.Count;
        }
    }
}
