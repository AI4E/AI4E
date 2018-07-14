using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleUpdated : DomainEvent<ModuleIdentifier>
    {
        public ModuleUpdated(ModuleIdentifier aggregateId, ModuleVersion oldVersion, ModuleVersion updatedVersion) : base(aggregateId)
        {
            OldVersion = oldVersion;
            UpdatedVersion = updatedVersion;
        }

        public ModuleVersion OldVersion { get; }
        public ModuleVersion UpdatedVersion { get; }
    }
}
