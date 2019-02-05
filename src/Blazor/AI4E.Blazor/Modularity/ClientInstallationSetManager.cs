using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static System.Diagnostics.Debug;

namespace AI4E.Blazor.Modularity
{
    internal sealed class ClientInstallationSetManager : InstallationSetManager
    {
        private const string _reloadBrowserMethod = "ai4e.reloadBrowser";
        private readonly IServiceProvider _serviceProvider;

        #region Fields

        private readonly ApplicationPartManager _partManager;
        private readonly ILogger<ClientInstallationSetManager> _logger;
        
        private ISet<ModuleIdentifier> _running = new HashSet<ModuleIdentifier>();

        private readonly ImmutableList<string> _hostInstalledAssemblies;
        private ImmutableDictionary<string, (Version version, bool isAppPart, ModuleIdentifier module)> _installedAssemblies;

        #endregion

        #region C'tor

        public ClientInstallationSetManager(
            ApplicationPartManager partManager,
            IServiceProvider serviceProvider,
            ILogger<ClientInstallationSetManager> logger = null)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (partManager == null)
                throw new ArgumentNullException(nameof(partManager));

            _serviceProvider = serviceProvider;
            _partManager = partManager;
            _logger = logger;

            var installedAssemblyBuilder = ImmutableDictionary.CreateBuilder<string, (Version version, bool isAppPart, ModuleIdentifier module)>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var isAppPart = partManager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == asm);

                installedAssemblyBuilder.Add(asm.GetName().Name, (asm.GetName().Version, isAppPart, ModuleIdentifier.UnknownModule));

                Console.WriteLine(asm.GetName().Name + " " + asm.GetName().Version);
            }

            _installedAssemblies = installedAssemblyBuilder.ToImmutable();
            _hostInstalledAssemblies = _installedAssemblies.Keys.ToImmutableList();
        }

        #endregion

        protected override async Task<bool> UpdateAsync(CancellationToken cancellation)
        {
            // Build new running set
            var installedModules = InstallationSet.Except(_running).ToList();
            var uninstalledModules = _running.Except(InstallationSet).ToList();

            Assert(!installedModules.Intersect(uninstalledModules).Any());

            if (!installedModules.Any() && !uninstalledModules.Any())
            {
                return false;
            }

            await UpdateAsync(installedModules, uninstalledModules, cancellation);

            _running = new HashSet<ModuleIdentifier>(InstallationSet);
            return true;
        }

        private async Task UpdateAsync(
            List<ModuleIdentifier> installedModules,
            List<ModuleIdentifier> uninstalledModules,
            CancellationToken cancellation)
        {
            _logger?.LogInformation("Updating module installation set.");

            if (uninstalledModules.Any())
            {
                _logger.LogWarning("Uninstalling module. As we cannot uninstall modules, we are reloading now.");
                await ReloadBrowserAsync();
                Environment.Exit(0);
            }

            var registerAsAppPart = new HashSet<string>();
            var installedAssemblies = _installedAssemblies.ToBuilder();

            using (var scope = _serviceProvider.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var moduleManifestProvider = serviceProvider.GetRequiredService<IModuleManifestProvider>();
                var moduleAssemblyDownloader = serviceProvider.GetRequiredService<IModuleAssemblyDownloader>();
                await ProcessModulesAsync(installedModules, registerAsAppPart, installedAssemblies, moduleManifestProvider, cancellation);
                await ProcessAssembliesAsync(installedAssemblies, moduleAssemblyDownloader, cancellation);
                ProcessAppParts(registerAsAppPart, moduleAssemblyDownloader);
            }

            _installedAssemblies = installedAssemblies.ToImmutable();
        }

        private async Task ProcessModulesAsync(
            List<ModuleIdentifier> installedModules,
            HashSet<string> registerAsAppPart,
            ImmutableDictionary<string, (Version version, bool isAppPart, ModuleIdentifier module)>.Builder installedAssemblies,
            IModuleManifestProvider moduleManifestProvider,
            CancellationToken cancellation)
        {
            foreach (var installedModule in installedModules)
            {
                _logger?.LogDebug($"Processing newly installed module {installedModule}.");

                var manifest = await moduleManifestProvider.GetModuleManifestAsync(installedModule, cancellation);
                var assemblies = manifest.Assemblies;

                foreach (var assembly in assemblies)
                {
                    var reload = ProcessModuleAssembly(assembly, installedModule, installedAssemblies, registerAsAppPart);

                    if (reload)
                    {
                        _logger?.LogWarning($"Cannnot install assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule} " +
                            $"as a minor version of the assembly is already installed that cannot be unloaded. " +
                            $"We are reloading now.");

                        await ReloadBrowserAsync();
                        Environment.Exit(0);
                    }
                }
            }
        }

        private bool ProcessModuleAssembly(
            BlazorModuleManifestAssemblyEntry assembly,
            ModuleIdentifier installedModule,
            ImmutableDictionary<string, (Version version, bool isAppPart, ModuleIdentifier module)>.Builder installedAssemblies,
            HashSet<string> registerAsAppPart)
        {
            _logger?.LogDebug($"Processing assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule}.");

            // If no assembly with the same name name exists, we can just add it.
            if (!installedAssemblies.TryGetValue(assembly.AssemblyName, out var existing))
            {
                _logger?.LogDebug($"Successfully processed assembly {assembly.AssemblyName} {assembly.AssemblyVersion}. The assembly is {(!assembly.IsAppPart ? "not " : "")} an app-part.");

                installedAssemblies[assembly.AssemblyName] = (assembly.AssemblyVersion, assembly.IsAppPart, installedModule);

                if (assembly.IsAppPart)
                {
                    registerAsAppPart.Add(assembly.AssemblyName);
                }

                return false;
            }

            // The version of the existing assembly is greater or equal than the one, we try to install.
            if (existing.version >= assembly.AssemblyVersion)
            {
                _logger?.LogDebug($"Successfully processed assembly {assembly.AssemblyName} {assembly.AssemblyVersion}. " +
                    $"It is not necessary to install the assembly as it will already be installed in version {existing.version}. " +
                    $"The assembly is {(!(existing.isAppPart || assembly.IsAppPart) ? "not " : "")} an app-part.");

                // TODO: If the versions match, we could add ourself to the list of modules that the assembly can be loaded from. (Optional)

                if (!existing.isAppPart && assembly.IsAppPart)
                {
                    registerAsAppPart.Add(assembly.AssemblyName);
                    installedAssemblies[assembly.AssemblyName] = (existing.version, isAppPart: true, existing.module);
                }

                return false;
            }

            // If the existing assembly is not yet installed.
            if (!_installedAssemblies.TryGetValue(assembly.AssemblyName, out var existingInstalled))
            {
                _logger?.LogDebug($"Successfully processed assembly {assembly.AssemblyName} {assembly.AssemblyVersion}. " +
                    $"Assembly will replace version {existing.version} in the future installation set. " +
                    $"The assembly is {(!(existing.isAppPart || assembly.IsAppPart) ? "not " : "")} an app-part.");

                // We are installing an assembly with a greater version than the existing and the existing is not yet installed.
                installedAssemblies[assembly.AssemblyName] = (assembly.AssemblyVersion, existing.isAppPart || assembly.IsAppPart, installedModule);

                if (assembly.IsAppPart)
                {
                    registerAsAppPart.Add(assembly.AssemblyName);
                }

                return false;
            }

            Assert(existingInstalled.version < assembly.AssemblyVersion);
            // This cannot happen, as we cannot unload (uninstall) assemblies.
            // The version of the existing (installed) assembly is greater or equal than the one, we try to install.
            //if (existingInstalled.version >= assembly.AssemblyVersion)
            //{
            //    if (!existingInstalled.isAppPart && assembly.IsAppPart)
            //    {
            //        registerAsAppPart.Add(assembly.AssemblyName);
            //        installedAssemblies[assembly.AssemblyName] = (existingInstalled.version, isAppPart: true, existingInstalled.module);
            //    }

            //    return false;
            //}

            // If loaded assembly is part of the hosts app, refreshing the browser is useless, as it will be loaded again in the exact same version.
            // For now we just ignore this version mismatch in the hope that everthing works fine.
            // TODO: Can we do anything about this? Maybe hijack the initial app load?
            if (_hostInstalledAssemblies.Any(p => p == assembly.AssemblyName))
            {
                _logger.LogWarning($"Cannnot install assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule} " +
                                    $"as a minor version of the assembly is already installed by the host app, that cannot be unloaded. Ignoring the failure in good hope.");

                if (!existingInstalled.isAppPart && assembly.IsAppPart)
                {
                    registerAsAppPart.Add(assembly.AssemblyName);
                    installedAssemblies[assembly.AssemblyName] = (existingInstalled.version, isAppPart: true, existing.module);
                }

                return false;
            }

            return true;
        }

        private async Task ProcessAssembliesAsync(
                   ImmutableDictionary<string, (Version version, bool isAppPart, ModuleIdentifier module)>.Builder installedAssemblies,
                   IModuleAssemblyDownloader moduleAssemblyDownloader,
                   CancellationToken cancellation)
        {
            // Download and install all new assemblies.
            foreach (var assembly in installedAssemblies)
            {
                if (_installedAssemblies.ContainsKey(assembly.Key))
                {
                    continue;
                }

                await moduleAssemblyDownloader.InstallAssemblyAsync(assembly.Value.module, assembly.Key, cancellation);
            }
        }

        private void ProcessAppParts(HashSet<string> registerAsAppPart, IModuleAssemblyDownloader moduleAssemblyDownloader)
        {
            foreach (var asmName in registerAsAppPart)
            {
                _logger?.LogDebug($"Installing {asmName} as app part.");

                var asm = moduleAssemblyDownloader.GetAssembly(asmName);

                Assert(asm != null);

                if (asm != null)
                {
                    var assemblyPart = new AssemblyPart(asm);
                    _partManager.ApplicationParts.Add(assemblyPart);
                }
            }
        }

        private Task ReloadBrowserAsync()
        {
            return JSRuntime.Current.InvokeAsync<object>(_reloadBrowserMethod);
        }
    }
}
