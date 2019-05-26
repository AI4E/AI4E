using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IModulePropertiesLookup
    {
        ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
