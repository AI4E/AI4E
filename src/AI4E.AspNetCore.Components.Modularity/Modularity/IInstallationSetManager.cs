using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    internal interface IInstallationSetManager
    {
        event EventHandler InstallationSetChanged;

        Task UpdateInstallationSetAsync(IEnumerable<ModuleIdentifier> installationSet, CancellationToken cancellation);
        Task InstallAsync(ModuleIdentifier module, CancellationToken cancellation);
        Task UninstallAsync(ModuleIdentifier module, CancellationToken cancellation);
    }
}
