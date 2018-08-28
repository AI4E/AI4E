using System;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Modularity.Debug;
using AI4E.Modularity.Host;

namespace AI4E.Blazor.Modularity
{
    public sealed class InstallationSetChangedEventHandler
    {
        private readonly IInstallationSetManager _installationSetManager;

        public InstallationSetChangedEventHandler(IInstallationSetManager installationSetManager)
        {
            if (installationSetManager == null)
                throw new ArgumentNullException(nameof(installationSetManager));

            _installationSetManager = installationSetManager;
        }

        public async Task HandleAsync(InstallationSetChanged message)
        {
            var installationSet = message.InstallationSet.Resolved.Select(p => p.Module);

            await _installationSetManager.UpdateInstallationSetAsync(installationSet, cancellation: default);
        }
    }

    public sealed class DebugModuleEventHandler
    {
        private readonly IInstallationSetManager _installationSetManager;

        public DebugModuleEventHandler(IInstallationSetManager installationSetManager)
        {
            if (installationSetManager == null)
                throw new ArgumentNullException(nameof(installationSetManager));

            _installationSetManager = installationSetManager;
        }

        public async Task HandleAsync(DebugModuleConnected mesage)
        {
            Console.WriteLine($"Debug module connected: {mesage.Module.Name}");

            try
            {
                await _installationSetManager.InstallAsync(mesage.Module, cancellation: default);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.ToString());
            }

        }
    }
}
