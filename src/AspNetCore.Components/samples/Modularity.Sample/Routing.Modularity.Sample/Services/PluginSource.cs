using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Modularity;
using AI4E.Messaging;
using Newtonsoft.Json;
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
                var pluginPath = GetPluginPath(pluginName);
                var blazorConfig = BlazorConfig.Read(pluginPath);
                var distPath = blazorConfig.DistPath;
                var blazorBootPath = Path.Combine(distPath, "_framework", "blazor.boot.json");

                BlazorBoot blazorBoot;

                using (var fileStream = new FileStream(blazorBootPath, FileMode.Open))
                using (var streamReader = new StreamReader(fileStream))
                {
                    blazorBoot = (BlazorBoot)JsonSerializer.CreateDefault().Deserialize(streamReader, typeof(BlazorBoot));
                }

                var binPath = Path.Combine(distPath, "_framework", "_bin");
                var moduleDescriptorBuilder = BlazorModuleDescriptor.CreateBuilder(pluginName, string.Empty);

                moduleDescriptorBuilder.StartupType = new SerializableType(pluginName + ".Startup");

                foreach (var assembly in GetAssemblies(blazorBoot))
                {
                    var assemblyLocation = Path.Combine(binPath, assembly!);

                    if (!File.Exists(assemblyLocation))
                    {
                        // TODO: Is this an error?
                        continue;
                    }

                    var assemblyName = AssemblyName.GetAssemblyName(assemblyLocation);

                    async ValueTask<BlazorModuleAssemblySource> LoadAssemblySourceAsync(CancellationToken cancellation)
                    {
                        using var assemblyStream = new FileStream(
                            assemblyLocation,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            bufferSize: 4096,
                            useAsync: true);

                        Debug.Assert(assemblyStream.CanSeek);
                        var assemblyBytesOwner = MemoryPool<byte>.Shared.RentExact(checked((int)assemblyStream.Length));

                        try
                        {
                            await assemblyStream.ReadExactAsync(assemblyBytesOwner.Memory, cancellation);

                            var symbolsLocation = Path.ChangeExtension(assemblyLocation, "pdb");

                            if (!File.Exists(symbolsLocation))
                            {
                                return new BlazorModuleAssemblySource(assemblyBytesOwner);
                            }

                            using var symbolsStream = new FileStream(
                                symbolsLocation,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read,
                                bufferSize: 4096,
                                useAsync: true);

                            Debug.Assert(symbolsStream.CanSeek);
                            var symbolsBytesOwner = MemoryPool<byte>.Shared.RentExact(checked((int)symbolsStream.Length));

                            try
                            {
                                await symbolsStream.ReadExactAsync(symbolsBytesOwner.Memory, cancellation);
                                return new BlazorModuleAssemblySource(assemblyBytesOwner, symbolsBytesOwner);
                            }
                            catch
                            {
                                symbolsBytesOwner.Dispose();
                                throw;
                            }
                        }
                        catch
                        {
                            assemblyBytesOwner.Dispose();
                            throw;
                        }
                    }

                    Debug.Assert(assemblyName.Version != null);

                    var moduleAssemblyDescriptorBuilder = BlazorModuleAssemblyDescriptor.CreateBuilder(
                        assemblyName.FullName,
                        assemblyName.Version!,
                        LoadAssemblySourceAsync);

                    moduleAssemblyDescriptorBuilder.IsComponentAssembly = (assembly == blazorBoot.Main);
                    moduleDescriptorBuilder.Assemblies.Add(moduleAssemblyDescriptorBuilder);
                }

                var moduleDescriptor = moduleDescriptorBuilder.Build();
                var plugin = new Plugin(pluginManager, moduleDescriptor);
                availablePluginsBuilder.Add(plugin);
            }

            return availablePluginsBuilder.ToImmutable();
        }

        private static IEnumerable<string> GetAssemblies(BlazorBoot blazorBoot)
        {
            return blazorBoot
                .AssemblyReferences
                .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Append(blazorBoot.Main);
        }

         private static string GetPluginPath(string pluginName)
        {
            var dir = Assembly.GetExecutingAssembly().Location;
            dir = GetDirectoryName(dir);
            var configuration = Path.GetFileName(dir = GetDirectoryName(dir));
            dir = GetDirectoryName(dir);
            dir = GetDirectoryName(dir);

            return Path.Combine(dir, pluginName, configuration, "netstandard2.1", pluginName + ".dll");
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
