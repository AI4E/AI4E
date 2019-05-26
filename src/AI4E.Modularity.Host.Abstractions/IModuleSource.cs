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
        #region Workaround // TODO

        // This is needed currently in order for the domain storage engine to read the properties 
        // from the respective entities as the domain storage engine uses the statically known type for property access.

        string Id { get; }
        long Revision { get; set; }
        string ConcurrencyToken { get; set; }

        #endregion

        ModuleSourceName Name { get; }

        Task<IEnumerable<ModuleReleaseIdentifier>> GetAvailableAsync(string searchPhrase,
                                                                     bool includePreReleases,
                                                                     IMetadataReader moduleMetadataReader,
                                                                     CancellationToken cancellation = default);

        ValueTask<IModuleMetadata> GetMetadataAsync(ModuleIdentifier module,
                                                    ModuleVersion version,
                                                    IMetadataReader moduleMetadataReader,
                                                    CancellationToken cancellation = default);

        ValueTask<DirectoryInfo> ExtractAsync(DirectoryInfo directory,
                                              ModuleReleaseIdentifier module,
                                              IMetadataReader moduleMetadataReader,
                                              CancellationToken cancellation = default);
    }
}
