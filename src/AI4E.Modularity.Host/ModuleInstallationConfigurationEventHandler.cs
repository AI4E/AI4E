using System;
using System.Threading.Tasks;
using AI4E.Modularity.Host;

namespace AI4E.Modularity.Host
{
    public sealed class ModuleInstallationConfigurationEventHandler : MessageHandler
    {
        private readonly IModuleManager _moduleManager;

        public ModuleInstallationConfigurationEventHandler(IModuleManager moduleManager)
        {
            if (moduleManager == null)
                throw new ArgumentNullException(nameof(moduleManager));

            _moduleManager = moduleManager;
        }

        // TODO: Message de-duplication and ordering
        public Task HandleAsync(InstallationSetChanged message)
        {
            var installationSet = message.InstallationSet;

            return _moduleManager.ConfigureInstallationSetAsync(installationSet, cancellation: default);
        }
    }
}
