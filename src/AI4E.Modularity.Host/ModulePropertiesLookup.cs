using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Memory;

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

        public async ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var prefixes = await _runningModules.GetPrefixesAsync(module, cancellation);
            var endPoints = await _runningModules.GetEndPointsAsync(module, cancellation);

            return new ModuleProperties(
                prefixes.Select(p => p.ConvertToString()).ToImmutableList(),
                endPoints.ToImmutableList());
        }
    }
}
