using System;

namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleSourceRemoveCommand : ConcurrencySafeCommand
    {
        public ModuleSourceRemoveCommand(Guid id, string concurrencyToken) : base(id, concurrencyToken) { }
    }
}
