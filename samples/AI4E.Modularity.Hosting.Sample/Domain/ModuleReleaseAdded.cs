using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleReleaseAdded : DomainEvent<ModuleIdentifier>
    {
        public ModuleReleaseAdded(ModuleIdentifier aggregateId, ModuleVersion version) : base(aggregateId)
        {
            Version = version;
        }

        public ModuleVersion Version { get; }
    }
}
