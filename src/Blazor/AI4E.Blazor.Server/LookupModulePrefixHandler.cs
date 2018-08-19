using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Blazor.Modularity;
using AI4E.Modularity;

namespace AI4E.Blazor.Server
{
    public sealed class LookupModulePrefixHandler : MessageHandler
    {
        private readonly IRunningModuleLookup _runningModules;

        public LookupModulePrefixHandler(IRunningModuleLookup runningModules)
        {
            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            _runningModules = runningModules;
        }

        public async Task<string> HandleAsync(LookupModulePrefix message)
        {
            var prefixes = await _runningModules.GetPrefixesAsync(message.Module, cancellation: default); // TODO: Cancellation

            // TODO: Send all prefixes?
            return prefixes.FirstOrDefault();
        }
    }
}
