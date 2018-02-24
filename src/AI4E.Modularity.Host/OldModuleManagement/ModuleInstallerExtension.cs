using System;
using System.Linq;

namespace AI4E.Modularity
{
    public static class ModuleInstallerExtension
    {
        public static bool IsInstalled(this IModuleInstaller moduleInstaller, ModuleReleaseIdentifier moduleRelease)
        {
            if (moduleInstaller == null)
                throw new ArgumentNullException(nameof(moduleInstaller));

            return moduleInstaller.InstalledModules.Any(p => p.Module == moduleRelease.Module && p.Version == moduleRelease.Version);
        }
    }
}
