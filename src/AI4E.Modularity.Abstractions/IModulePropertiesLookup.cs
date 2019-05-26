using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Metadata;

namespace AI4E.Modularity
{
    public interface IModulePropertiesLookup
    {
        ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
