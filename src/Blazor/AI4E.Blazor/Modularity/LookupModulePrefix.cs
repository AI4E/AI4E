using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    public sealed class LookupModulePrefix
    {
        public LookupModulePrefix(ModuleIdentifier module)
        {
            Module = module;
        }

        public ModuleIdentifier Module { get; }
    }
}
