using System;

namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleSourceUpdateLocationCommand : ConcurrencySafeCommand
    {
        public ModuleSourceUpdateLocationCommand(Guid id, string concurrencyToken, string location) : base(id, concurrencyToken)
        {
            Location = location;
        }

        public string Location { get; }
    }
}
