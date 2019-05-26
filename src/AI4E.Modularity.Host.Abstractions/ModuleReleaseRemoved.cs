namespace AI4E.Modularity.Host
{
    public sealed class ModuleReleaseRemoved
    {
        public ModuleReleaseRemoved(ModuleIdentifier moduleId, ModuleVersion version)
        {
            ModuleId = moduleId;
            Version = version;
        }

        public ModuleIdentifier ModuleId { get; }
        public ModuleVersion Version { get; }
    }
}
