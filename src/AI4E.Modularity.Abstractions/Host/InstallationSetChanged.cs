namespace AI4E.Modularity.Host
{
    public sealed class InstallationSetChanged
    {
        public InstallationSetChanged(ResolvedInstallationSet installationSet)
        {
            InstallationSet = installationSet;
        }

        public ResolvedInstallationSet InstallationSet { get; }
    }
}
