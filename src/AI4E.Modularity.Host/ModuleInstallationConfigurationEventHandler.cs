using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleInstallationConfigurationEventHandler : MessageHandler
    {
        private readonly IModuleInstallationManager _moduleManager;

        public ModuleInstallationConfigurationEventHandler(IModuleInstallationManager moduleManager)
        {
            if (moduleManager == null)
                throw new ArgumentNullException(nameof(moduleManager));

            _moduleManager = moduleManager;
        }

        // TODO: Message de-duplication and ordering
        public Task HandleAsync(InstallationSetChanged message, CancellationToken cancellation)
        {
            var installationSet = message.InstallationSet;

            return _moduleManager.ConfigureInstallationSetAsync(installationSet, cancellation);
        }
    }
}
