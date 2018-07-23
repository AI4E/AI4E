using System.Collections.Generic;
using AI4E.Modularity.Host;
using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage.Projection;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    using Module = Host.Module;

    public sealed class ModuleProjection : Projection
    {
        [NoProjectionMember]
        public ModuleListModel ProjectToListModel(IModule module, bool includePreReleases)
        {
            var latestRelease = module.GetLatestRelease(includePreReleases);

            Assert(latestRelease != null);

            return new ModuleListModel
            {
                Id = module.Id,
                LatestVersion = latestRelease.Version,
                InstalledVersion = module.InstalledVersion
            };
        }

        public ModuleListModel ProjectToListModel(Module module)
        {
            return ProjectToListModel(module, includePreReleases: true);
        }

        public IEnumerable<ModuleReleaseModel> ProjectToReleaseModels(Module module)
        {
            // This cannot be done with yield as the "availableVersions" list does not contain all necessary entries then. The releases had to be evaluated twice.
            var result = new List<ModuleReleaseModel>();
            var availableVersions = new List<AvailableVersionModel>();

            foreach (var release in module.Releases)
            {
                Assert(release != null);
                Assert(release.Module != null);

                result.Add(Project(release, availableVersions));
                availableVersions.Add(ProjectToAvailableVersionModel(release));
            }

            return result;
        }

        [NoProjectionMember]
        private ModuleReleaseModel Project(ModuleRelease release, List<AvailableVersionModel> availableVersions)
        {
            return new ModuleReleaseModel
            {
                Id = release.Id,
                Version = release.Version,
                ReleaseDate = release.ReleaseDate,
                Name = release.Name,
                Author = release.Author,
                Description = release.Description,
                AvailableVersions = availableVersions
            };
        }

        [NoProjectionMember]
        private AvailableVersionModel ProjectToAvailableVersionModel(ModuleRelease release)
        {
            return new AvailableVersionModel
            {
                ReleaseDate = release.ReleaseDate,
                Version = release.Version,
                IsInstalled = release.IsInstalled
            };
        }

        public ModuleUninstallModel ProjectToUninstallModel(Module module)
        {
            // If the module is not installed, we do not need an uninstall model.
            if (!module.IsInstalled)
            {
                return null;
            }

            return new ModuleUninstallModel
            {
                Id = module.Id,
                ConcurrencyToken = module.ConcurrencyToken,
                Name = module.InstalledRelease.Name
            };
        }

        public IEnumerable<ModuleInstallModel> ProjectToInstallModels(Module module)
        {
            foreach (var release in module.Releases)
            {
                // If the release is installed, we need no install model for it.
                if (release.IsInstalled)
                {
                    continue;
                }

                yield return new ModuleInstallModel
                {
                    Id = release.Id,
                    ConcurrencyToken = module.ConcurrencyToken,
                    Name = release.Name
                };
            }
        }
    }
}
