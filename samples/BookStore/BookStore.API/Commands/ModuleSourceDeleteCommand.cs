using System;
using AI4E;

namespace BookStore.Commands
{
    public sealed class ModuleSourceDeleteCommand : ConcurrencySafeCommand
    {
        public ModuleSourceDeleteCommand(Guid id, string concurrencyToken)
            : base(id, concurrencyToken) { }
    }
}
