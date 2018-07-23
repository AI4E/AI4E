using System;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleSourceRemoved
    {
        public ModuleSourceRemoved(Guid moduleSourceId)
        {
            ModuleSourceId = moduleSourceId;
        }

        public Guid ModuleSourceId { get; }
    }
}
