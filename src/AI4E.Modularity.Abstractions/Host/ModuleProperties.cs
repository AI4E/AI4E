using System.Collections.Immutable;
using AI4E.Routing;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleProperties
    {
        public ModuleProperties(ImmutableList<string> prefixes, ImmutableList<EndPointAddress> endPoints)
        {
            Prefixes = prefixes;
            EndPoints = endPoints;
        }

        public ImmutableList<string> Prefixes { get; }
        public ImmutableList<EndPointAddress> EndPoints { get; }
    }

    public sealed class ModulePropertiesQuery : Query<ModuleProperties>
    {
        public ModulePropertiesQuery(ModuleIdentifier module)
        {
            Module = module;
        }

        public ModuleIdentifier Module { get; }
    }
}
