using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class InstallationSetChanged : DomainEvent<SingletonId>
    {
        public InstallationSetChanged(ResolvedInstallationSet installationSet) : base(default)
        {
            InstallationSet = installationSet;
        }

        public ResolvedInstallationSet InstallationSet { get; }
    }
}
