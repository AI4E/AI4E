using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleInstalled : DomainEvent<ModuleIdentifier>
    {
        public ModuleInstalled(ModuleIdentifier aggregateId, ModuleVersion version) : base(aggregateId)
        {
            Version = version;
        }

        public ModuleVersion Version { get; }
    }
}
