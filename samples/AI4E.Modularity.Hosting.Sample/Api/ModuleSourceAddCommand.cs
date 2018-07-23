using System;

namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleSourceAddCommand : Command
    {
        public ModuleSourceAddCommand(Guid id, string name, string location) : base(id)
        {
            Name = name;
            Location = location;
        }

        public string Name { get; }
        public string Location { get; }
    }
}
