using AI4E.Routing;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugModule
    {
        public DebugModule(EndPointRoute endPoint, ModuleIdentifier module, ModuleVersion moduleVersion)
        {
            EndPoint = endPoint;
            Module = module;
            ModuleVersion = moduleVersion;
        }

        public EndPointRoute EndPoint { get; }
        public ModuleIdentifier Module { get; }
        public ModuleVersion ModuleVersion { get; }
    }
}
