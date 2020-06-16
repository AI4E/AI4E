using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using AI4E.AspNetCore.Components.Modularity;

namespace Routing.Modularity.Sample.Services
{
    public sealed class PluginManager 
    {
        private readonly ImmutableHashSet<Plugin> _availablePlugins;
        private ImmutableHashSet<Plugin> _installedPlugins = ImmutableHashSet<Plugin>.Empty;

        public PluginManager()
        {
            _availablePlugins = PluginSource.Instance.GetAvailablePlugins(this);
        }

        public IReadOnlyCollection<Plugin> AvailablePlugins => _availablePlugins;

        public IReadOnlyCollection<Plugin> InstalledPlugins => Volatile.Read(ref _installedPlugins);

        internal void Install(Plugin plugin)
        {
            ImmutableHashSet<Plugin> current = Volatile.Read(ref _installedPlugins), start, desired;

            do
            {
                start = current;
                desired = start.Add(plugin);
                if (desired == start)
                    return;
                current = Interlocked.CompareExchange(ref _installedPlugins, desired, start);
            }
            while (start != current);

            OnPluginsChanged();
        }

        internal void Uninstall(Plugin plugin)
        {
            ImmutableHashSet<Plugin> current = Volatile.Read(ref _installedPlugins), start, desired;

            do
            {
                start = current;
                desired = start.Remove(plugin);
                if (desired == start)
                    return;
                current = Interlocked.CompareExchange(ref _installedPlugins, desired, start);
            }
            while (start != current);

            OnPluginsChanged();
        }

        internal bool IsInstalled(Plugin plugin)
        {
            var plugins = Volatile.Read(ref _installedPlugins);
            return plugins.Contains(plugin);
        }

        public event EventHandler? InstalledPluginsChanged;

        private void OnPluginsChanged()
        {
            InstalledPluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        public readonly struct Plugin : IEquatable<Plugin>
        {
            private readonly PluginManager _pluginManager;

            internal Plugin(PluginManager pluginManager, IBlazorModuleDescriptor blazorModuleDescriptor)
            {
                _pluginManager = pluginManager;
                ModuleDescriptor = blazorModuleDescriptor;
            }

            public bool IsInstalled => _pluginManager.IsInstalled(this);
            public IBlazorModuleDescriptor ModuleDescriptor { get; }

            public void Install()
            {
                _pluginManager.Install(this);
            }

            public void Uninstall()
            {
                _pluginManager.Uninstall(this);
            }

            public bool Equals(in Plugin other)
            {
                return (_pluginManager, ModuleDescriptor) == (other._pluginManager, other.ModuleDescriptor);
            }

            public bool Equals(Plugin other)
            {
                return Equals(in other);
            }

            public override bool Equals(object? obj)
            {
                return obj is Plugin plugin && Equals(in plugin);
            }

            public override int GetHashCode()
            {
                return (_pluginManager, ModuleDescriptor).GetHashCode();
            }

            public static bool operator ==(in Plugin left, in Plugin right)
            {
                return left.Equals(in right);
            }

            public static bool operator !=(in Plugin left, in Plugin right)
            {
                return !left.Equals(in right);
            }
        }
    }
}
