using AI4E.Modularity.Hosting.Sample.Models;
using AI4E.Storage.Projection;

namespace AI4E.Modularity.Hosting.Sample.Services
{
    using Module = Domain.Module;

    public sealed class ModuleProjection : Projection
    {
        [NoProjectionMember]
        public ModuleListModel ProjectToListModel(Module module, bool includePreReleases)
        {
            var latestRelease = module.GetLatestRelease(includePreReleases);

            return new ModuleListModel
            {
                Id = module.Id.ToString(),
                LatestVersion = latestRelease.Version.ToString(),
                IsPreRelease = latestRelease.Version.IsPreRelease
            };
        }

        public ModuleListModel ProjectToListModel(Module module)
        {
            return ProjectToListModel(module, includePreReleases: true);
        }
    }
}
