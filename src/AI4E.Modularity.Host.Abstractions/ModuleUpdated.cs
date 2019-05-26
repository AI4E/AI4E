using AI4E.Modularity.Metadata;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleUpdated 
    {
        public ModuleUpdated(ModuleIdentifier moduleId, ModuleVersion oldVersion, ModuleVersion updatedVersion)
        {
            ModuleId = moduleId;
            OldVersion = oldVersion;
            UpdatedVersion = updatedVersion;
        }

        public ModuleIdentifier ModuleId { get; }
        public ModuleVersion OldVersion { get; }
        public ModuleVersion UpdatedVersion { get; }
    }
}
