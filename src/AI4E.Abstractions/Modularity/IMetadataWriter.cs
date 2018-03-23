using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IMetadataWriter
    {
        Task WriteMetadataAsync(Stream stream, IModuleMetadata metadata, CancellationToken cancellation);
    }
}
