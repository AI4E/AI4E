using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public static class ModuleSourceExtension
    {
        public static ValueTask<IModuleMetadata> GetMetadataAsync(this IModuleSource moduleSource,
                                                                  ModuleReleaseIdentifier moduleRelease,
                                                                  IMetadataReader moduleMetadataReader,
                                                                  CancellationToken cancellation = default)
        {
            if (moduleSource == null)
                throw new ArgumentNullException(nameof(moduleSource));

            if (moduleRelease == default)
                throw new ArgumentDefaultException(nameof(moduleRelease));

            return moduleSource.GetMetadataAsync(moduleRelease.Module, moduleRelease.Version, moduleMetadataReader, cancellation);
        }
    }
}
