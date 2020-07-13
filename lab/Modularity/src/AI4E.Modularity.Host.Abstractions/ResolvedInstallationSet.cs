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
using System.Collections.Immutable;
using System.Linq;
using AI4E.Modularity.Metadata;
using Newtonsoft.Json;

namespace AI4E.Modularity.Host
{
    public readonly struct ResolvedInstallationSet : IComparable<ResolvedInstallationSet>
    {
        public static ResolvedInstallationSet EmptyInstallationSet { get; } = default;

        private readonly ImmutableDictionary<ModuleIdentifier, ModuleVersion> _resolved;

        [JsonConstructor]
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

        [JsonProperty("Resolved")]
        public IEnumerable<ModuleReleaseIdentifier> Resolved => (_resolved?.Select(p => new ModuleReleaseIdentifier(p.Key, p.Value))
                                                                                 ?? Enumerable.Empty<ModuleReleaseIdentifier>()).ToList();

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
