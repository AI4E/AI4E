namespace AI4E.Modularity.Debug
{
#if BLAZOR
    internal
#else
    public
#endif
    sealed class DebugModuleDisconnected
    {
        public DebugModuleDisconnected(DebugModuleProperties moduleProperties)
        {
            ModuleProperties = moduleProperties;
        }

        public DebugModuleProperties ModuleProperties { get; }
    }
}
