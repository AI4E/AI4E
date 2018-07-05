using System;

namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleSourceRenameCommand : ConcurrencySafeCommand
    {
        public ModuleSourceRenameCommand(Guid id, string concurrencyToken, string name) : base(id, concurrencyToken)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
