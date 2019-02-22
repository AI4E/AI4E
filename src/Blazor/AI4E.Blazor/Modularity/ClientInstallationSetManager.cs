using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Blazor.Modularity
{
    internal sealed class ClientInstallationSetManager : InstallationSetManager
    {
        private const string _reloadBrowserMethod = "ai4e.reloadBrowser";

        #region Fields

        private readonly ApplicationPartManager _partManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InstallationSetManager> _logger;
        private readonly AsyncLock _lock = new AsyncLock();

        private ISet<ModuleIdentifier> _running = new HashSet<ModuleIdentifier>();
        private readonly ISet<ModuleIdentifier> _inclusiveModules = new HashSet<ModuleIdentifier>();
        private readonly ISet<ModuleIdentifier> _exclusiveModules = new HashSet<ModuleIdentifier>();

        private ImmutableDictionary<string, (Version version, bool isAppPart, ModuleIdentifier module)> _installedAssemblies;

        #endregion

        #region C'tor

        public ClientInstallationSetManager(
            ApplicationPartManager partManager,
            IServiceProvider serviceProvider,
            ILogger<InstallationSetManager> logger = null)
        {
            if (partManager == null)
                throw new ArgumentNullException(nameof(partManager));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _partManager = partManager;
            _serviceProvider = serviceProvider;
            _logger = logger;

            var installedAssemblyBuilder = ImmutableDictionary.CreateBuilder<string, (Version version, bool isAppPart, ModuleIdentifier module)>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var isAppPart = partManager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == asm);

                installedAssemblyBuilder.Add(asm.GetName().Name, (asm.GetName().Version, isAppPart, ModuleIdentifier.UnknownModule));

                Console.WriteLine(asm.GetName().Name + " " + asm.GetName().Version);
            }

            _installedAssemblies = installedAssemblyBuilder.ToImmutable();
        }

        #endregion

        #region InstallationSetManager

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

        #endregion

        private async Task UpdateAsync(
            List<ModuleIdentifier> installedModules,
            List<ModuleIdentifier> uninstalledModules,
            CancellationToken cancellation)
        {
            _logger?.LogInformation("Updating module installation set.");

            if (uninstalledModules.Any())
            {
                _logger.LogWarning("Uninstalling module. As we cannot uninstall modules, we are reloading now.");
                ReloadBrowser();
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

                var manifest = await LoadManifestAsync(installedModule, moduleManifestProvider, cancellation);
                var assemblies = manifest.Assemblies;

                foreach (var assembly in assemblies)
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

                        continue;
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

                        continue;
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

                        continue;
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

                    //    continue;
                    //}

                    var reload = true;

                    // If loaded assembly is part of the hosts app, refreshing the browser is useless, as it will be loaded again in the exact same version.
                    // For now we just ignore this version mismatch in the hope that everthing works fine.
                    // TODO: Can we do anything about this? Maybe hijack the initial app load?
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name == assembly.AssemblyName)
                        {
                            _logger.LogWarning($"Cannnot install assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule} " +
                            $"as a minor version of the assembly is already installed by the host app, that cannot be unloaded. Ignoring the failure in good hope.");

                            if (!existingInstalled.isAppPart && assembly.IsAppPart)
                            {
                                registerAsAppPart.Add(assembly.AssemblyName);
                                installedAssemblies[assembly.AssemblyName] = (existingInstalled.version, isAppPart: true, existing.module);
                            }

                            reload = false;
                            break;
                        }
                    }

                    if (reload)
                    {
                        // TODO: Reload browser
                        _logger?.LogWarning($"Cannnot install assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule} " +
                            $"as a minor version of the assembly is already installed that cannot be unloaded. " +
                            $"We are reloading now.");

                        ReloadBrowser();
                        Environment.Exit(0);
                    }
                }
            }
        }

        private async Task ProcessAssembliesAsync(
            ImmutableDictionary<string, (Version version, bool isAppPart, ModuleIdentifier module)>.Builder installedAssemblies,
            IModuleAssemblyDownloader moduleAssemblyDownloader,
            CancellationToken cancellation)
        {
            foreach (var assembly in installedAssemblies)
            {
                if (_installedAssemblies.ContainsKey(assembly.Key))
                {
                    continue;
                }

                await InstallAssemblyAsync(assembly, moduleAssemblyDownloader, cancellation);
            }
        }

        private void ProcessAppParts(
            HashSet<string> registerAsAppPart,
            IModuleAssemblyDownloader moduleAssemblyDownloader)
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

        private async ValueTask InstallAssemblyAsync(
            KeyValuePair<string, (Version version, bool isAppPart, ModuleIdentifier module)> assembly,
            IModuleAssemblyDownloader moduleAssemblyDownloader,
            CancellationToken cancellation)
        {
            Assembly asm;
            do
            {
                asm = await moduleAssemblyDownloader.InstallAssemblyAsync(assembly.Value.module, assembly.Key, cancellation);
            }
            while (asm == null); // TODO: Should we throw an exception and abort instead of retrying this forever?
        }

        private async ValueTask<BlazorModuleManifest> LoadManifestAsync(
            ModuleIdentifier installedModule,
            IModuleManifestProvider moduleManifestProvider,
            CancellationToken cancellation)
        {
            BlazorModuleManifest manifest;

            do
            {
                manifest = await moduleManifestProvider.GetModuleManifestAsync(installedModule, cancellation);
            }
            while (manifest == null);  // TODO: Should we throw an exception and abort instead of retrying this forever?

            return manifest;
        }

        private void ReloadBrowser()
        {
            if (JSRuntime.Current is IJSInProcessRuntime jSInProcessRuntime)
            {
                jSInProcessRuntime.Invoke<object>(_reloadBrowserMethod);
            }
            else
            {
                JSRuntime.Current.InvokeAsync<object>(_reloadBrowserMethod).GetAwaiter().GetResult();
            }
        }
    }
}
