using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Metadata
{
    public interface IMetadataAccessor
    {
        ValueTask<IModuleMetadata> GetMetadataAsync(Assembly entryAssembly, CancellationToken cancellation = default);
    }

    public static class MetadataAccessorExtension
    {
        public static ValueTask<IModuleMetadata> GetMetadataAsync(this IMetadataAccessor metadataAccessor, CancellationToken cancellation = default)
        {
            if (metadataAccessor == null)
                throw new ArgumentNullException(nameof(metadataAccessor));

            var entryAssembly = Assembly.GetEntryAssembly();
            return metadataAccessor.GetMetadataAsync(entryAssembly, cancellation);
        }
    }
}
