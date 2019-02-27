using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public sealed class ModulePropertiesLookup : IModulePropertiesLookup
    {
        private readonly IModuleManager _runningModules;

        public ModulePropertiesLookup(IModuleManager runningModules)
        {
            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            _runningModules = runningModules;
        }

        public ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            return _runningModules.GetPropertiesAsync(module, cancellation);
        }
    }
}
