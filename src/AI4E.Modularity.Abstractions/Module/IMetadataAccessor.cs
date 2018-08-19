using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Module
{
    public interface IMetadataAccessor
    {
        ValueTask<IModuleMetadata> GetMetadataAsync(CancellationToken cancellation);
    }
}