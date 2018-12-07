using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Host;

namespace AI4E.Blazor.Modularity
{
    [MessageHandler]
    internal sealed class InstallationSetChangedEventHandler
    {
        private readonly IInstallationSetManager _installationSetManager;

        public InstallationSetChangedEventHandler(IInstallationSetManager installationSetManager)
        {
            if (installationSetManager == null)
                throw new ArgumentNullException(nameof(installationSetManager));

            _installationSetManager = installationSetManager;
        }

        public async Task HandleAsync(InstallationSetChanged message, CancellationToken cancellation)
        {
            var installationSet = message.InstallationSet.Resolved.Select(p => p.Module);

            await _installationSetManager.UpdateInstallationSetAsync(installationSet, cancellation);
        }
    }
}
