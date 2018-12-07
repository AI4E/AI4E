using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity.Debug;
using Microsoft.Extensions.Logging;

namespace AI4E.Blazor.Modularity
{
    [MessageHandler]
    internal sealed class DebugModuleEventHandler
    {
        private readonly IInstallationSetManager _installationSetManager;
        private readonly ILogger<DebugModuleEventHandler> _logger;

        public DebugModuleEventHandler(IInstallationSetManager installationSetManager, ILogger<DebugModuleEventHandler> logger = null)
        {
            if (installationSetManager == null)
                throw new ArgumentNullException(nameof(installationSetManager));

            _installationSetManager = installationSetManager;
            _logger = logger;
        }

        public Task HandleAsync(DebugModuleConnected message, CancellationToken cancellation)
        {
            _logger?.LogDebug($"Debug module connected: {message.ModuleProperties.Module.Name}");
            return _installationSetManager.InstallAsync(message.ModuleProperties.Module, cancellation);
        }

        public Task HandleAsync(DebugModuleDisconnected message, CancellationToken cancellation)
        {
            _logger?.LogDebug($"Debug module disconnected: {message.ModuleProperties.Module.Name}");
            return _installationSetManager.UninstallAsync(message.ModuleProperties.Module, cancellation);
        }
    }
}
