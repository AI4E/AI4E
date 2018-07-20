namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleUninstallModel
    {
        public ModuleIdentifier Id { get; set; }
        public string ConcurrencyToken { get; set; }

        public string Name { get; set; }
    }
}
