namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleInstallCommand : ConcurrencySafeCommand<ModuleIdentifier>
    {
        public ModuleInstallCommand(ModuleIdentifier id, string concurrencyToken, ModuleVersion version) : base(id, concurrencyToken)
        {
            Version = version;
        }

        public ModuleVersion Version { get; }
    }
}
