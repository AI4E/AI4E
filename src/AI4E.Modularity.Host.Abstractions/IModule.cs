using System.Collections.Generic;

namespace AI4E.Modularity.Host
{
    public interface IModule
    {
        ModuleIdentifier Id { get; }

        IModuleRelease InstalledRelease { get; }
        ModuleVersion? InstalledVersion { get; }
        bool IsInstalled { get; }
        bool IsLatestReleaseInstalled { get; }
        IModuleRelease LatestRelease { get; }
        ModuleVersion LatestVersion { get; }
        IEnumerable<IModuleRelease> Releases { get; }

        IModuleRelease AddRelease(IModuleMetadata metadata, IModuleSource moduleSource);
        IModuleRelease GetLatestRelease(bool includePreReleases);
        IEnumerable<IModuleRelease> GetMatchingReleases(ModuleVersionRange versionRange);
        IModuleRelease GetRelease(ModuleVersion version);
        void Install();
        void Uninstall();
    }
}