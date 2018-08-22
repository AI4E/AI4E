using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    public interface IModulePrefixLookup
    {
        ValueTask<string> LookupPrefixAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
