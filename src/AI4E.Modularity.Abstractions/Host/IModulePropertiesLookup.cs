using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public interface IModulePropertiesLookup
    {
        ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
