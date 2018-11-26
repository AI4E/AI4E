using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Routing;

namespace AI4E.Blazor.Modularity
{
    internal interface IModulePrefixLookup
    {
        ValueTask<string> LookupPrefixAsync(ModuleIdentifier module, CancellationToken cancellation);
    }

    internal interface IModuleEndPointLookup
    {
        ValueTask<EndPointAddress> LookupEndPointAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
