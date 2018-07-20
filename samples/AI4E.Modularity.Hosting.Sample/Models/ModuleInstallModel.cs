namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleInstallModel
    {
        public ModuleReleaseIdentifier Id { get; set; }
        public string ConcurrencyToken { get; set; }

        public string Name { get; set; }
        public ModuleVersion Version => Id.Version;
    }
}
