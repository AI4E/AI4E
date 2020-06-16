using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AI4E.AspNetCore.Components.Modularity;

namespace Routing.Modularity.Sample.Services
{
    public sealed class PluginModuleSource : IBlazorModuleSource
    {
        private readonly PluginManager _pluginManager;

        public PluginModuleSource(PluginManager pluginManager)
        {
            if (pluginManager is null)
                throw new ArgumentNullException(nameof(pluginManager));

            _pluginManager = pluginManager;
        }

        public event EventHandler? ModulesChanged
        {
            add
            {
                _pluginManager.InstalledPluginsChanged += value;
            }
            remove
            {
                _pluginManager.InstalledPluginsChanged -= value;
            }
        }

        public IAsyncEnumerable<IBlazorModuleDescriptor> GetModulesAsync(CancellationToken cancellation)
        {
            return _pluginManager.InstalledPlugins.Select(p => p.ModuleDescriptor).ToAsyncEnumerable();
        }
    }
}
