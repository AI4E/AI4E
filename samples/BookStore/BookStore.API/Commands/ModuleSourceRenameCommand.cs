using System;
using AI4E;

namespace BookStore.Commands
{
    public sealed class ModuleSourceRenameCommand : ConcurrencySafeCommand
    {
        public ModuleSourceRenameCommand(Guid id, string concurrencyToken, string name)
            : base(id, concurrencyToken)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
