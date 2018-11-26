namespace AI4E.Modularity.Host
{
#if BLAZOR
    internal
#else
    public
#endif
    sealed class InstallationSetChanged
    {
        public InstallationSetChanged(ResolvedInstallationSet installationSet)
        {
            InstallationSet = installationSet;
        }

        public ResolvedInstallationSet InstallationSet { get; }
    }
}
