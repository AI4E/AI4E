using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IMetadataReader
    {
        Task<IModuleMetadata> ReadMetadataAsync(Stream stream, CancellationToken cancellation);
    }
}
