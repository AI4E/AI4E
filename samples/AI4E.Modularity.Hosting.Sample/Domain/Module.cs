using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Domain;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class Module : AggregateRoot<ModuleIdentifier>
    {
        private readonly Dictionary<ModuleVersion, ModuleRelease> _releases;

        public Module(IModuleMetadata metadata, FileSystemModuleSource moduleSource) : base(metadata.Module)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (moduleSource == null)
                throw new ArgumentNullException(nameof(moduleSource));

            _releases = new Dictionary<ModuleVersion, ModuleRelease>()
            {
                [metadata.Version] = new ModuleRelease(this, metadata, moduleSource)
            };
        }

        [JsonConstructor]
        private Module(ModuleIdentifier id, IEnumerable<ModuleRelease> releases) : base(id)
        {
            _releases = new Dictionary<ModuleVersion, ModuleRelease>();

            foreach (var release in releases)
            {
                _releases.Add(release.Version, release);
            }
        }

        public IEnumerable<ModuleRelease> Releases => _releases.Values.ToImmutableList();

        public ModuleRelease InstalledRelease { get; private set; }

        [JsonIgnore]
        public ModuleRelease LatestRelease => _releases.Values.OrderByDescending(p => p.Version).First();

        [JsonIgnore]
        public ModuleVersion LatestVersion => LatestRelease.Version;

        [JsonIgnore]
        public ModuleVersion? InstalledVersion => InstalledRelease?.Version;

        [JsonIgnore]
        public bool IsInstalled => InstalledRelease != null;

        [JsonIgnore]
        public bool IsLatestReleaseInstalled => LatestRelease.IsInstalled;

        public ModuleRelease GetLatestRelease(bool includePreReleases)
        {
            if (includePreReleases)
            {
                return LatestRelease;
            }

            return _releases.Values.Where(p => !p.Version.IsPreRelease).OrderByDescending(p => p.Version).FirstOrDefault();
        }

        public ModuleRelease GetModuleRelease(ModuleVersion version)
        {
            return _releases.TryGetValue(version, out var result) ? result : null;
        }

        public ModuleRelease AddModuleRelease(IModuleMetadata metadata, FileSystemModuleSource moduleSource)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (moduleSource == null)
                throw new ArgumentNullException(nameof(moduleSource));

            var version = metadata.Version;
            var release = new ModuleRelease(this, metadata, moduleSource);

            if (_releases.TryAdd(version, release))
            {
                return release;
            }

            release = _releases[version];
            release.AddSource(moduleSource);
            return release;
        }

        internal void RemoveModuleRelease(ModuleRelease release)
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

    public sealed class ModuleInstalled : DomainEvent<ModuleIdentifier>
    {
        public ModuleInstalled(ModuleIdentifier aggregateId, ModuleVersion version) : base(aggregateId)
        {
            Version = version;
        }

        public ModuleVersion Version { get; }
    }

    public sealed class ModuleUninstalled : DomainEvent<ModuleIdentifier>
    {
        public ModuleUninstalled(ModuleIdentifier aggregateId, ModuleVersion version) : base(aggregateId)
        {
            Version = version;
        }

        public ModuleVersion Version { get; }
    }

    public sealed class ModuleUpdated : DomainEvent<ModuleIdentifier>
    {
        public ModuleUpdated(ModuleIdentifier aggregateId, ModuleVersion oldVersion, ModuleVersion updatedVersion) : base(aggregateId)
        {
            OldVersion = oldVersion;
            UpdatedVersion = updatedVersion;
        }

        public ModuleVersion OldVersion { get; }
        public ModuleVersion UpdatedVersion { get; }
    }
}
