namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleListModel
    {
        public ModuleIdentifier Id { get; set; }
        public ModuleVersion LatestVersion { get; set; }
        public bool IsPreRelease => LatestVersion.IsPreRelease;
    }
}
