using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class InstallationSetConflict : DomainEvent<SingletonId>
    {
        public InstallationSetConflict() : base(default) { }
    }
}
