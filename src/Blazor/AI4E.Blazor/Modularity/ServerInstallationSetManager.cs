//#define SUPPORT_UNLOADING
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Blazor.Modularity
{
    public sealed class ServerInstallationSetManager : InstallationSetManager
    {
        private readonly ApplicationPartManager _partManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServerInstallationSetManager> _logger;
        private Dictionary<ModuleIdentifier, AssemblyLoadContext> _running = new Dictionary<ModuleIdentifier, AssemblyLoadContext>();

        private readonly ImmutableList<AssemblyName> _hostInstalledAssemblies;

        public ServerInstallationSetManager(ApplicationPartManager partManager, IServiceProvider serviceProvider, ILogger<ServerInstallationSetManager> logger = null)
        {
            if (partManager == null)
                throw new ArgumentNullException(nameof(partManager));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _partManager = partManager;
            _serviceProvider = serviceProvider;
            _logger = logger;

            var hostInstalledAssembliesBuilder = ImmutableList.CreateBuilder<AssemblyName>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                hostInstalledAssembliesBuilder.Add(asm.GetName());
            }

            _hostInstalledAssemblies = hostInstalledAssembliesBuilder.ToImmutable();
        }

        protected override async Task<bool> UpdateAsync(CancellationToken cancellation)
        {
            // Build new running set
            var installedModules = InstallationSet.Except(_running.Keys).ToList();
            var uninstalledModules = _running.Keys.Except(InstallationSet).ToList();

            Assert(!installedModules.Intersect(uninstalledModules).Any());

            if (!installedModules.Any() && !uninstalledModules.Any())
            {
                return false;
            }

            await UpdateAsync(installedModules, uninstalledModules, cancellation);

            return true;
        }

        private async Task UpdateAsync(
            List<ModuleIdentifier> installedModules,
            List<ModuleIdentifier> uninstalledModules,
            CancellationToken cancellation)
        {
            _logger?.LogInformation("Updating module installation set.");

            foreach (var module in uninstalledModules)
            {
                UninstallModule(module);
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var moduleManifestProvider = serviceProvider.GetRequiredService<IModuleManifestProvider>();
                var moduleAssemblyDownloader = serviceProvider.GetRequiredService<IModuleAssemblyDownloader>();

                foreach (var module in installedModules)
                {
                    await InstallModuleAsync(module, moduleManifestProvider, moduleAssemblyDownloader, cancellation);
                }
            }
        }

        private async Task InstallModuleAsync(
            ModuleIdentifier module,
            IModuleManifestProvider moduleManifestProvider,
            IModuleAssemblyDownloader moduleAssemblyDownloader,
            CancellationToken cancellation)
        {
            _logger?.LogDebug($"Processing newly installed module {module}.");

            var manifest = await moduleManifestProvider.GetModuleManifestAsync(module, cancellation);
            var assemblies = manifest.Assemblies;

            bool IsPartOfHost(BlazorModuleManifestAssemblyEntry assembly)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(p => p.GetName().Name.Equals(assembly.AssemblyName, StringComparison.Ordinal));

                if (asm != null &&
                   AssemblyLoadContext.GetLoadContext(asm) == AssemblyLoadContext.Default &&
                   asm.GetName().Version >= assembly.AssemblyVersion)
                {
                    return true;
                }

                return false;
            }

            async Task<(BlazorModuleManifestAssemblyEntry assembly, byte[] assemblyBytes)> DownloadAssemblyAsync(
                BlazorModuleManifestAssemblyEntry assembly)
            {
                var assemblyBytes = await moduleAssemblyDownloader.DownloadAssemblyAsync(module, assembly.AssemblyName, cancellation);
                return (assembly, assemblyBytes);
            }

            AssemblyName GetAssemblyName(BlazorModuleManifestAssemblyEntry assembly)
            {
                return new AssemblyName { Name = assembly.AssemblyName, Version = assembly.AssemblyVersion };
            }

            var loadedAssemblies = new List<(BlazorModuleManifestAssemblyEntry assembly, byte[] assemblyBytes)>();

            foreach (var assembly in assemblies.Where(assembly => !IsPartOfHost(assembly)))
            {
                loadedAssemblies.Add(await DownloadAssemblyAsync(assembly));
            }

#if SUPPORT_UNLOADING
            var loadContext = new ModuleAssemblyLoadContext(loadedAssemblies.ToImmutableDictionary(p => GetAssemblyName(p.assembly), p => p.assemblyBytes));
#else
            var loadContext = AssemblyLoadContext.Default;

            foreach (var (asm, bytes) in loadedAssemblies)
            {
                using (var memoryStream = new MemoryStream(bytes))
                {
                    loadContext.LoadFromStream(memoryStream);
                }
            }
#endif
            foreach (var asmName in assemblies.Where(p => p.IsAppPart).Select(GetAssemblyName))
            {
                var asm = loadContext.LoadFromAssemblyName(asmName);

                _logger?.LogDebug($"Installing {asmName} as app part.");

                Assert(asm != null);

                if (asm != null)
                {
                    var assemblyPart = new AssemblyPart(asm);
                    _partManager.ApplicationParts.Add(assemblyPart);
                }
            }

            _running.Add(module, loadContext);
        }

        private void UninstallModule(ModuleIdentifier module)
        {
#if SUPPORT_UNLOADING
            var loadContextWeak = UninstallModuleCore(module);

            var i = 0;
            for (; loadContextWeak.TryGetTarget(out _) && i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (i == 10)
            {
                // TODO
            }
#else
            throw new NotSupportedException("Module unloading is not supported.");
#endif
        }

        private WeakReference<AssemblyLoadContext> UninstallModuleCore(ModuleIdentifier module)
        {
            var loadContext = _running[module];
            var loadContextWeak = new WeakReference<AssemblyLoadContext>(loadContext, trackResurrection: true);
            _running.Remove(module);

            // TODO: loadContext.Unload();

            return loadContextWeak;
        }

        private sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly ImmutableDictionary<AssemblyName, byte[]> _assemblies;
            private readonly Dictionary<AssemblyName, Assembly> _assemblyLookup = new Dictionary<AssemblyName, Assembly>(AssemblyNameEqualityComparer.Instance);

            public ModuleAssemblyLoadContext(ImmutableDictionary<AssemblyName, byte[]> assemblies)
            {
                _assemblies = assemblies.WithComparers(AssemblyNameEqualityComparer.Instance);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (_assemblyLookup.TryGetValue(assemblyName, out var assembly))
                {
                    return assembly;
                }

                if (_assemblies.TryGetValue(assemblyName, out var assemblyBytes))
                {
                    Console.WriteLine("---> Loading assembly " + assemblyName.Name + " " + assemblyName.Version); // TODO: Remove me

                    using (var stream = new MemoryStream(assemblyBytes))
                    {
                        assembly = LoadFromStream(stream);
                    }

                    _assemblyLookup.Add(assemblyName, assembly);
                    return assembly;
                }

                Console.WriteLine("---> Loading assembly " + assemblyName.Name + " " + assemblyName.Version + " from default context."); // TODO: Remove me
                return null;
            }
        }

        private sealed class AssemblyNameEqualityComparer : IEqualityComparer<AssemblyName>
        {
            public static AssemblyNameEqualityComparer Instance { get; } = new AssemblyNameEqualityComparer();

            private AssemblyNameEqualityComparer() { }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                return x.Name.Equals(y.Name, StringComparison.Ordinal) && x.Version == y.Version;
            }

            public int GetHashCode(AssemblyName obj)
            {
                return (obj.Name, obj.Version).GetHashCode();
            }
        }
    }
}
