using System;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleSourceAdded
    {
        public ModuleSourceAdded(Guid moduleSourceId, string location)
        {
            ModuleSourceId = moduleSourceId;
            Location = location;
        }

        public Guid ModuleSourceId { get; }
        public string Location { get; }
    }
}
