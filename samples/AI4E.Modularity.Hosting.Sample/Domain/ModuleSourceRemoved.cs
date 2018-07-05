using System;
using AI4E.Domain;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public sealed class ModuleSourceRemoved : DomainEvent
    {
        public ModuleSourceRemoved(Guid aggregateId) : base(aggregateId) { }
    }
}
