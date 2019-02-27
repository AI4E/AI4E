using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    [MessageHandler]
    internal sealed class ModulePropertiesQueryHandler
    {
        private readonly IModulePropertiesLookup _modulePropertiesLookup;

        public ModulePropertiesQueryHandler(IModulePropertiesLookup modulePropertiesLookup)
        {
            if (modulePropertiesLookup == null)
                throw new ArgumentNullException(nameof(modulePropertiesLookup));

            _modulePropertiesLookup = modulePropertiesLookup;
        }

        public ValueTask<ModuleProperties> HandleAsync(ModulePropertiesQuery message, CancellationToken cancellation)
        {
            return _modulePropertiesLookup.LookupAsync(message.Module, cancellation);
        }
    }
}
