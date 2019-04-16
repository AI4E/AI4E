using System;
using AI4E;

namespace BookStore.Commands
{
    public sealed class ModuleSourceCreateCommand : Command
    {
        public ModuleSourceCreateCommand(Guid id, string name, string location)
            : base(id)
        {
            Name = name;
            Location = location;
        }

        public string Name { get; }
        public string Location { get; }
    }
}
