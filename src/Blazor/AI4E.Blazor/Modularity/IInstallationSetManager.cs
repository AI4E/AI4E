using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    public interface IInstallationSetManager
    {
        Task UpdateInstallationSetAsync(IEnumerable<ModuleIdentifier> installationSet, CancellationToken cancellation);
        Task InstallAsync(ModuleIdentifier module, CancellationToken cancellation);
        Task UninstallAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
