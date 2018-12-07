using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Blazor.Modularity;
using AI4E.Internal;
using AI4E.Modularity;

namespace AI4E.Blazor.Server
{
    [MessageHandler]
    internal sealed class LookupModulePrefixHandler : MessageHandler
    {
        private readonly IModuleManager _runningModules;

        public LookupModulePrefixHandler(IModuleManager runningModules)
        {
            if (runningModules == null)
                throw new ArgumentNullException(nameof(runningModules));

            _runningModules = runningModules;
        }

        public async Task<string> HandleAsync(LookupModulePrefix message, CancellationToken cancellation)
        {
            var prefixes = await _runningModules.GetPrefixesAsync(message.Module, cancellation);

            // TODO: Send all prefixes?
            return prefixes.Select(p => p.ConvertToString()).FirstOrDefault();
        }
    }
}
