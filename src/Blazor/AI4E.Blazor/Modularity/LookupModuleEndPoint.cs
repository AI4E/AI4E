using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    internal sealed class LookupModuleEndPoint
    {
        public LookupModuleEndPoint(ModuleIdentifier module)
        {
            Module = module;
        }

        public ModuleIdentifier Module { get; }
    }
}
