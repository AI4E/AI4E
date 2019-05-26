using AI4E.Modularity.Metadata;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleUninstalled
    {
        public ModuleUninstalled(ModuleIdentifier moduleId, ModuleVersion version)
        {
            ModuleId = moduleId;
            Version = version;
        }

        public ModuleIdentifier ModuleId { get; }

        public ModuleVersion Version { get; }
    }
}
