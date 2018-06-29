using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI4E.Modularity
{
    public interface IModuleManager
    {
        Task<IEnumerable<IModule>> GetModulesAsync(bool includePreReleases = false);
        Task<IModule> GetModuleAsync(ModuleIdentifier moduleIdentifier);
        Task<IModuleRelease> GetModuleReleaseAsync(ModuleIdentifier moduleIdentifier, ModuleVersion moduleVersion = default);
        Task<IModuleRelease> GetModuleReleaseAsync(ModuleReleaseIdentifier moduleReleaseIdentifier);
        Task<IEnumerable<IModuleRelease>> GetUpdatesAsync(bool includePreReleases = false);
        Task<IEnumerable<IModuleRelease>> GetInstalledAsync();
        //Task<IEnumerable<IModule>> GetDebugModulesAsync();

        // Module sources
        Task<IEnumerable<IModuleSource>> GetModuleSourcesAsync();
        Task<IModuleSource> GetModuleSourceAsync(string name);
        Task AddModuleSourceAsync(string name, string source);
        Task RemoveModuleSourceAsync(string name);
        Task UpdateModuleSourceAsync(string name, string source);
    }
}
