using System;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Modularity.Metadata;
using AI4E.Routing;

namespace AI4E.Modularity
{
    public sealed class ModuleProperties
    {
        public ModuleProperties(ImmutableList<string> prefixes, ImmutableList<EndPointAddress> endPoints)
        {
            if (!prefixes.Any())
                throw new ArgumentException("The collection must contain at least one entry", nameof(prefixes));

            if(!endPoints.Any())
                throw new ArgumentException("The collection must contain at least one entry", nameof(endPoints));

            if (prefixes.Any(prefix => string.IsNullOrWhiteSpace(prefix)))
                throw new ArgumentException("The collection must not contain null entries or entries that are empty or contain whitespace only.", nameof(prefixes));

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
