using System;
using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleSourceAdded : DomainEvent
    {
        public ModuleSourceAdded(Guid aggregateId, ModuleSourceLocation location) : base(aggregateId)
        {
            if (location == default)
                throw new ArgumentDefaultException(nameof(location));

            Location = location;
        }

        public ModuleSourceLocation Location { get; }
    }
}
