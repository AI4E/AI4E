using System;
using AI4E.AspNetCore.Components.Modularity;

namespace Routing.Modularity.Sample.Services
{
    public sealed class PluginModuleSourceFactory : IBlazorModuleSourceFactory
    {
        private readonly PluginManager _pluginManager;

        public PluginModuleSourceFactory(PluginManager pluginManager)
        {
            if (pluginManager is null)
                throw new ArgumentNullException(nameof(pluginManager));

            _pluginManager = pluginManager;
        }

        public IBlazorModuleSource CreateModuleSource()
        {
            return new PluginModuleSource(_pluginManager);
        }
    }
}
