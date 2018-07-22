using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public readonly struct UnresolvedInstallationSet
    {
        private readonly ImmutableDictionary<ModuleIdentifier, ModuleVersion> _resolved;
        private readonly ImmutableDictionary<ModuleIdentifier, ModuleVersionRange> _unresolved;

        [JsonConstructor]
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

        [JsonProperty("Resolved")]
        public IEnumerable<ModuleReleaseIdentifier> Resolved => (_resolved?.Select(p => new ModuleReleaseIdentifier(p.Key, p.Value))
                                                                          ?? Enumerable.Empty<ModuleReleaseIdentifier>()).ToList();

        [JsonProperty("Unresolved")]
        public IEnumerable<ModuleDependency> Unresolved => (_unresolved?.Select(p => new ModuleDependency(p.Key, p.Value))
                                                                       ?? Enumerable.Empty<ModuleDependency>()).ToList();

        public async Task<IEnumerable<ResolvedInstallationSet>> ResolveAsync(IDependencyResolver dependencyResolver, CancellationToken cancellation)
        {
            if (dependencyResolver == null)
                throw new ArgumentNullException(nameof(dependencyResolver));

            if (TryGetUnresolved(out var dependency))
            {
                Assert(_unresolved != null);

                // A conflict occured
                var result = await TryResolveSingleDependencyAsync(dependency, dependencyResolver, cancellation);

                return result;
            }

            return new ResolvedInstallationSet(Resolved).Yield();
        }

        private bool TryGetUnresolved(out ModuleDependency dependency)
        {
            if (_unresolved == null)
            {
                dependency = default;
                return false;
            }

            var modules = _unresolved.Keys;

            if (!modules.Any())
            {
                dependency = default;
                return false;
            }

            var module = modules.First();
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
            return (await Task.WhenAll(resolveTasks)).SelectMany(_ => _).Concat(resolvedInstallationSets);
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

        public bool ContainsModule(ModuleIdentifier module)
        {
            if (_resolved != null && _resolved.ContainsKey(module))
                return true;

            if (_unresolved != null && _unresolved.ContainsKey(module))
                return true;

            return false;
        }

        public UnresolvedInstallationSet WithUnresolved(ModuleIdentifier module, ModuleVersionRange versionRange)
        {
            if (ContainsModule(module))
                throw new InvalidOperationException();

            return new UnresolvedInstallationSet(_resolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersion>.Empty,
                                                 (_unresolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersionRange>.Empty).Add(module, versionRange));
        }

        public UnresolvedInstallationSet WithoutUnresolved(ModuleIdentifier module)
        {
            if (_resolved != null && _resolved.ContainsKey(module))
                throw new InvalidOperationException();

            return new UnresolvedInstallationSet(_resolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersion>.Empty,
                                                 (_unresolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersionRange>.Empty).Remove(module));
        }

        public UnresolvedInstallationSet SetVersionRange(ModuleIdentifier module, ModuleVersionRange versionRange)
        {
            if (_resolved != null && _resolved.ContainsKey(module))
                throw new InvalidOperationException();

            return new UnresolvedInstallationSet(_resolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersion>.Empty,
                                                 (_unresolved ?? ImmutableDictionary<ModuleIdentifier, ModuleVersionRange>.Empty).SetItem(module, versionRange));
        }
    }
}
