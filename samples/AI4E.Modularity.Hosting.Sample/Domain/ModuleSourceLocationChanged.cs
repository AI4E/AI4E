using System;
using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleSourceLocationChanged : DomainEvent
    {
        public ModuleSourceLocationChanged(Guid aggregateId, ModuleSourceLocation location) : base(aggregateId)
        {
            if (location == default)
                throw new ArgumentDefaultException(nameof(location));

            Location = location;
        }

        public ModuleSourceLocation Location { get; }
    }
}
