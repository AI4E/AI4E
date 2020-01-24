using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using AI4E.AspNetCore.Components.Modularity;
using AI4E.Messaging;
using AI4E.Utils;
using static Routing.Modularity.Sample.Services.PluginManager;

namespace Routing.Modularity.Sample.Services
{
    internal sealed class PluginSource
    {
        public static PluginSource Instance { get; } = new PluginSource();

        private PluginSource() { }

        private static readonly ImmutableArray<string> _pluginNames = new List<string>
        {
            "Routing.Modularity.Sample.PluginA",
            "Routing.Modularity.Sample.PluginB"
        }.ToImmutableArray();

        public ImmutableHashSet<Plugin> GetAvailablePlugins(PluginManager pluginManager)
        {
            var availablePluginsBuilder = ImmutableHashSet.CreateBuilder<Plugin>();

            foreach (var pluginName in _pluginNames)
            {
                var moduleDescriptor = BuildModuleDescriptor(pluginName);
                var plugin = new Plugin(pluginManager, moduleDescriptor);
                availablePluginsBuilder.Add(plugin);
            }

            return availablePluginsBuilder.ToImmutable();
        }

        private BlazorModuleDescriptor BuildModuleDescriptor(string pluginName)
        {
            var pluginPath = GetPluginPath(pluginName);
            var assemblyLocations = GetAssemblyLocations(pluginPath);
            var moduleDescriptorBuilder = BlazorModuleDescriptor.CreateBuilder(pluginName, urlPrefix: string.Empty);

            foreach (var assemblyLocation in assemblyLocations)
            {
                var assemblyName = AssemblyName.GetAssemblyName(assemblyLocation);

                var moduleAssemblyDescriptorBuilder = BlazorModuleAssemblyDescriptor.CreateBuilder(
                        assemblyName.FullName,
                        assemblyName.Version!,
                        cancellation => BlazorModuleAssemblySource.FromLocationAsync(assemblyLocation, forceLoad: false, cancellation));

                if (StringComparer.OrdinalIgnoreCase.Equals(assemblyName.Name, pluginName))
                {
                    moduleAssemblyDescriptorBuilder.IsComponentAssembly = true;
                }

                moduleDescriptorBuilder.Assemblies.Add(moduleAssemblyDescriptorBuilder);
            }

            moduleDescriptorBuilder.StartupType = new SerializableType(pluginName + ".Startup");

            return moduleDescriptorBuilder.Build();
        }

        private static readonly EnumerationOptions _assemblyEnumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            MatchType = MatchType.Simple,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        private static IEnumerable<string> GetAssemblyLocations(string pluginPath)
        {
            return Directory.EnumerateFiles(pluginPath, "*.dll", _assemblyEnumerationOptions);
        }

        private static string GetPluginPath(string pluginName)
        {
            var dir = Assembly.GetExecutingAssembly().Location;
            dir = GetDirectoryName(dir);
            var configuration = Path.GetFileName(dir = GetDirectoryName(dir));
            dir = GetDirectoryName(dir);
            dir = GetDirectoryName(dir);

            return Path.Combine(dir, pluginName, configuration, "netstandard2.1");
        }

        private static string GetDirectoryName(string path)
        {
            path = Path.GetDirectoryName(path)!;
            if (path.Last() == '/' || path.Last() == '\\')
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }
    }
}
