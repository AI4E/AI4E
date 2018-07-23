using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    // TODO: The IMetadataReader is a dependency of the implementation actually.
    //       Currently there is no support to use dependency injection in the domain model.
    //       If this support is added, the parameters can be removed.
    public interface IModuleSource
    {
        ModuleSourceName Name { get; }

        Task<IEnumerable<ModuleReleaseIdentifier>> GetAvailableAsync(string searchPhrase,
                                                                     bool includePreReleases,
                                                                     IMetadataReader moduleMetadataReader,
                                                                     CancellationToken cancellation);

        ValueTask<IModuleMetadata> GetMetadataAsync(ModuleReleaseIdentifier module,
                                                    IMetadataReader moduleMetadataReader,
                                                    CancellationToken cancellation);

        ValueTask<DirectoryInfo> ExtractAsync(DirectoryInfo directory,
                                              ModuleReleaseIdentifier module,
                                              IMetadataReader moduleMetadataReader,
                                              CancellationToken cancellation);
    }
}
