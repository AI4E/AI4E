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
using AI4E.Domain;
using AI4E.Modularity.Metadata;
using AI4E.Utils;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    // TODO: It is possible that there do exist modules without a release. A release is removed if its source gets deleted.
    public sealed class Module : AggregateRoot<ModuleIdentifier>, IModule
    {
        private readonly Dictionary<ModuleVersion, ModuleRelease> _releases;

        public Module(IModuleMetadata metadata, IModuleSource moduleSource) : base(metadata.Module)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (moduleSource == null)
                throw new ArgumentNullException(nameof(moduleSource));

            _releases = new Dictionary<ModuleVersion, ModuleRelease>()
            {
                [metadata.Version] = new ModuleRelease(this, metadata, moduleSource)
            };

            Notify(new ModuleReleaseAdded(Id, metadata.Version));
        }

        [JsonConstructor]
        private Module(ModuleIdentifier id, IEnumerable<ModuleRelease> releases) : base(id)
        {
            _releases = new Dictionary<ModuleVersion, ModuleRelease>();

            foreach (var release in releases)
            {
                // This should be set be the serializer actually. See https://github.com/JamesNK/Newtonsoft.Json/issues/1284
                release.Module = this;
                _releases.Add(release.Version, release);
            }
        }

        public IEnumerable<ModuleRelease> Releases => _releases.Values.ToImmutableList();

        IEnumerable<IModuleRelease> IModule.Releases => Releases;

        [JsonProperty("InstalledVersion")]
        private ModuleVersion? _installedVersion;

        // TODO: Why Json.Net does not serialize the reference directly?
        public ModuleRelease InstalledRelease
        {
            get => _installedVersion != null ? _releases[(ModuleVersion)_installedVersion] : null;
            private set => _installedVersion = value?.Version;
        }

        IModuleRelease IModule.InstalledRelease => InstalledRelease;

        [JsonIgnore]
        public ModuleRelease LatestRelease => _releases.Values.OrderByDescending(p => p.Version).First();

        IModuleRelease IModule.LatestRelease => LatestRelease;

        [JsonIgnore]
        public ModuleVersion LatestVersion => LatestRelease.Version;

        [JsonIgnore]
        public ModuleVersion? InstalledVersion => InstalledRelease?.Version;

        [JsonIgnore]
        public bool IsInstalled => InstalledRelease != null;

        [JsonIgnore]
        public bool IsLatestReleaseInstalled => LatestRelease.IsInstalled;

        IModuleRelease IModule.AddRelease(IModuleMetadata metadata, IModuleSource moduleSource)
        {
            return AddRelease(metadata, moduleSource);
        }

        IModuleRelease IModule.GetLatestRelease(bool includePreReleases)
        {
            return GetLatestRelease(includePreReleases);
        }

        IEnumerable<IModuleRelease> IModule.GetMatchingReleases(ModuleVersionRange versionRange)
        {
            return GetMatchingReleases(versionRange);
        }

        IModuleRelease IModule.GetRelease(ModuleVersion version)
        {
            return GetRelease(version);
        }

        public IEnumerable<ModuleRelease> GetMatchingReleases(ModuleVersionRange versionRange)
        {
            return Releases.Where(release => versionRange.IsMatch(release.Version));
        }

        public ModuleRelease GetLatestRelease(bool includePreReleases)
        {
            if (includePreReleases)
            {
                return LatestRelease;
            }

            return _releases.Values.Where(p => !p.Version.IsPreRelease).OrderByDescending(p => p.Version).FirstOrDefault();
        }

        public ModuleRelease GetRelease(ModuleVersion version)
        {
            return _releases.TryGetValue(version, out var result) ? result : null;
        }

        public ModuleRelease AddRelease(IModuleMetadata metadata, IModuleSource moduleSource)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (moduleSource == null)
                throw new ArgumentNullException(nameof(moduleSource));

            var version = metadata.Version;
            var release = new ModuleRelease(this, metadata, moduleSource);

            if (_releases.TryAdd(version, release))
            {
                Notify(new ModuleReleaseAdded(Id, version));
                return release;
            }

            release = _releases[version];
            release.TryAddSource(moduleSource);
            return release;
        }

        internal void RemoveRelease(ModuleRelease release)
        {
            Assert(release != null);
            Assert(release.Module == this);

            if (release.IsInstalled)
            {
                release.Uninstall();
            }

#if DEBUG
            Assert(_releases.TryGetValue(release.Version, out var value) && value == release);
#endif

            _releases.Remove(release.Version);
            Notify(new ModuleReleaseRemoved(Id, release.Version));
        }

        internal void Install(ModuleRelease release)
        {
            Assert(release != null);
            Assert(release.Module == this);

            if (release == InstalledRelease)
                return;

            var installedVersion = InstalledVersion;

            InstalledRelease = release;

            if (installedVersion != null)
            {
                Notify(new ModuleUpdated(Id, (ModuleVersion)installedVersion, release.Version));
            }
            else
            {
                Notify(new ModuleInstalled(Id, release.Version));
            }
        }

        public void Install()
        {
            Install(LatestRelease);
        }

        public void Uninstall()
        {
            var installedRelease = InstalledRelease;

            if (installedRelease == null)
                return;

            InstalledRelease = null;

            Notify(new ModuleUninstalled(Id, installedRelease.Version));
        }
    }
}
