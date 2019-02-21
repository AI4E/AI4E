namespace AI4E.Modularity.Debug
{
    public sealed class DebugModuleDisconnected
    {
        public DebugModuleDisconnected(DebugModuleProperties moduleProperties)
        {
            ModuleProperties = moduleProperties;
        }

        public DebugModuleProperties ModuleProperties { get; }
    }
}
