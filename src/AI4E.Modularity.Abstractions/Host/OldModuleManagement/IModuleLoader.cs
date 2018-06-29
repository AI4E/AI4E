using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IModuleLoader
    {
        Task<(Stream, IModuleMetadata)> LoadModuleAsync(ModuleReleaseIdentifier identifier);
        Task<IModuleMetadata> LoadModuleMetadataAsync(ModuleReleaseIdentifier identifier);
        Task<IEnumerable<ModuleReleaseIdentifier>> ListModulesAsync();
    }
}