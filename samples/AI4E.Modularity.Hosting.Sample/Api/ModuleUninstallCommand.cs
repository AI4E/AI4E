namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleUninstallCommand : ConcurrencySafeCommand<ModuleIdentifier>
    {
        public ModuleUninstallCommand(ModuleIdentifier id, string concurrencyToken) : base(id, concurrencyToken)
        {
        }
    }
}
