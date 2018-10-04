using AI4E.Routing;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugModule
    {
        public DebugModule(EndPointAddress endPoint, ModuleIdentifier module, ModuleVersion moduleVersion)
        {
            EndPoint = endPoint;
            Module = module;
            ModuleVersion = moduleVersion;
        }

        public EndPointAddress EndPoint { get; }
        public ModuleIdentifier Module { get; }
        public ModuleVersion ModuleVersion { get; }
    }
}
