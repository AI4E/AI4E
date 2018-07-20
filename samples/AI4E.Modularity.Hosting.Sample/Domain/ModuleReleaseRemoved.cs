using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleReleaseRemoved : DomainEvent<ModuleIdentifier>
    {
        public ModuleReleaseRemoved(ModuleIdentifier aggregateId, ModuleVersion version) : base(aggregateId)
        {
            Version = version;
        }

        public ModuleVersion Version { get; }
    }
}
