using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
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
