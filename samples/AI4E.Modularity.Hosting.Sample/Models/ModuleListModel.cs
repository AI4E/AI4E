namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleListModel
    {
        public ModuleIdentifier Id { get; set; }
        public ModuleVersion LatestVersion { get; set; }
        public ModuleVersion? InstalledVersion { get; set; }
        public bool IsInstalled => InstalledVersion != null;
        public bool IsPreRelease => LatestVersion.IsPreRelease;
        public bool IsUpdateAvailable => InstalledVersion < LatestVersion;
    }
}
