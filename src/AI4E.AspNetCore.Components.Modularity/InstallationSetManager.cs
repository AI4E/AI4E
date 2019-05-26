/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Modularity;
using AI4E.Modularity.Metadata;
using AI4E.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using static System.Diagnostics.Debug;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class InstallationSetManager : IInstallationSetManager, IDisposable
    {
        private const string _reloadBrowserMethod = "ai4e.reloadBrowser";
        private readonly IRunningModuleManager _runningModuleManager;
        private readonly IModuleAssemblyDownloader _moduleAssemblyDownloader;

        #region Fields

        private readonly IModuleManifestProvider _moduleManifestProvider;
        private readonly AssemblyManager _assemblyManager;
        private readonly IJSRuntime _jSRuntime;
        private readonly ILogger<InstallationSetManager> _logger;
        private ISet<ModuleIdentifier> _running = new HashSet<ModuleIdentifier>();

        private ImmutableDictionary<string, (Version version, bool isComponentAssembly, ModuleIdentifier module)> _installedAssemblies;
        private CancellationTokenSource _disposalCancellationSource = new CancellationTokenSource();

        #endregion

        #region C'tor

        public InstallationSetManager(
            IRunningModuleManager runningModuleManager,
            IModuleAssemblyDownloader moduleAssemblyDownloader,
            IModuleManifestProvider moduleManifestProvider,
            AssemblyManager assemblyManager,
            IJSRuntime jSRuntime,
            ILogger<InstallationSetManager> logger = null)
        {
            if (runningModuleManager == null)
                throw new ArgumentNullException(nameof(runningModuleManager));

            if (moduleAssemblyDownloader == null)
                throw new ArgumentNullException(nameof(moduleAssemblyDownloader));

            if (moduleManifestProvider == null)
                throw new ArgumentNullException(nameof(moduleManifestProvider));

            if (assemblyManager == null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (jSRuntime == null)
                throw new ArgumentNullException(nameof(jSRuntime));

            _runningModuleManager = runningModuleManager;
            _moduleAssemblyDownloader = moduleAssemblyDownloader;
            _moduleManifestProvider = moduleManifestProvider;
            _assemblyManager = assemblyManager;
            _jSRuntime = jSRuntime;
            _logger = logger;

            var installedAssemblyBuilder = ImmutableDictionary.CreateBuilder<string, (Version version, bool isComponentAssembly, ModuleIdentifier module)>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var isComponentAssembly = assemblyManager.Assemblies.Contains(asm);

                installedAssemblyBuilder.Add(asm.GetName().Name, (asm.GetName().Version, isComponentAssembly, ModuleIdentifier.UnknownModule));
            }

            _logger?.LogTrace("Initially loaded assemblies:\r\n" + installedAssemblyBuilder.Select(p => p.Key + " " + p.Value.version).Aggregate((e, n) => e + "\r\n" + n));

            _installedAssemblies = installedAssemblyBuilder.ToImmutable();

            runningModuleManager.ModuleStarted += UpdateInstallation;
            runningModuleManager.ModuleTerminated += UpdateInstallation;
        }

        private void UpdateInstallation(object sender, ModuleIdentifier e)
        {
            var cancellationSource = Volatile.Read(ref _disposalCancellationSource);

            if (cancellationSource != null)
            {
                UpdateAsync(cancellation: cancellationSource.Token).HandleExceptions(_logger);
            }
        }

        #endregion

        #region IInstallationSetManager

        public event EventHandler InstallationSetChanged;

        #endregion

        private async Task UpdateAsync(CancellationToken cancellation)
        {
            // Build new running set
            var installationSet = _runningModuleManager.Modules;
            var installedModules = installationSet.Except(_running).ToList();
            var uninstalledModules = _running.Except(installationSet).ToList();

            await UpdateAsync(installedModules, uninstalledModules, cancellation);

            _running = new HashSet<ModuleIdentifier>(installationSet);
            InstallationSetChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task UpdateAsync(
            List<ModuleIdentifier> installedModules,
            List<ModuleIdentifier> uninstalledModules,
            CancellationToken cancellation)
        {
            _logger?.LogInformation("Updating module installation set.");

            if (uninstalledModules.Any())
            {
                _logger?.LogWarning("Uninstalling module. As we cannot uninstall modules, we are reloading now.");
                ReloadBrowser();
                Environment.Exit(0);
            }

            var registerAsComponentAssembly = new HashSet<string>();
            var installedAssemblies = _installedAssemblies.ToBuilder();

            foreach (var installedModule in installedModules)
            {
                _logger?.LogDebug($"Processing newly installed module {installedModule}.");

                var manifest = await LoadManifestAsync(installedModule, cancellation);

                if (manifest == null)
                {
                    _logger?.LogWarning($"Unable to install {installedModule}. The module does not seem to have a manifest.");
                    continue;
                }

                var assemblies = manifest.Assemblies;

                foreach (var assembly in assemblies)
                {
                    _logger?.LogDebug($"Processing assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule}.");

                    // If no assembly with the same name name exists, we can just add it.
                    if (!installedAssemblies.TryGetValue(assembly.AssemblyName, out var existing))
                    {
                        _logger?.LogDebug($"Successfully processed assembly {assembly.AssemblyName} {assembly.AssemblyVersion}. The assembly is {(!assembly.IsComponentAssembly ? "not " : "")} an app-part.");

                        installedAssemblies[assembly.AssemblyName] = (assembly.AssemblyVersion, assembly.IsComponentAssembly, installedModule);

                        if (assembly.IsComponentAssembly)
                        {
                            registerAsComponentAssembly.Add(assembly.AssemblyName);
                        }

                        continue;
                    }

                    // The version of the existing assembly is greater or equal than the one, we try to install.
                    if (existing.version >= assembly.AssemblyVersion)
                    {
                        _logger?.LogDebug(
                            $"Successfully processed assembly {assembly.AssemblyName} {assembly.AssemblyVersion}. " +
                            $"It is not necessary to install the assembly as it will already be installed in version {existing.version}. " +
                            $"The assembly is {(!(existing.isComponentAssembly || assembly.IsComponentAssembly) ? "not " : "")} an app-part.");

                        // TODO: If the versions match, we could add ourself to the list of modules that the assembly can be loaded from. (Optional)

                        if (!existing.isComponentAssembly && assembly.IsComponentAssembly)
                        {
                            registerAsComponentAssembly.Add(assembly.AssemblyName);
                            installedAssemblies[assembly.AssemblyName] = (existing.version, isComponentAssembly: true, existing.module);
                        }

                        continue;
                    }

                    // If the existing assembly is not yet installed.
                    if (!_installedAssemblies.TryGetValue(assembly.AssemblyName, out var existingInstalled))
                    {
                        _logger?.LogDebug(
                            $"Successfully processed assembly {assembly.AssemblyName} {assembly.AssemblyVersion}. " +
                            $"Assembly will replace version {existing.version} in the future installation set. " +
                            $"The assembly is {(!(existing.isComponentAssembly || assembly.IsComponentAssembly) ? "not " : "")} an app-part.");

                        // We are installing an assembly with a greater version than the existing and the existing is not yet installed.
                        installedAssemblies[assembly.AssemblyName] = (assembly.AssemblyVersion, existing.isComponentAssembly || assembly.IsComponentAssembly, installedModule);

                        if (assembly.IsComponentAssembly)
                        {
                            registerAsComponentAssembly.Add(assembly.AssemblyName);
                        }

                        continue;
                    }

                    Assert(existingInstalled.version < assembly.AssemblyVersion);
                    // This cannot happen, as we cannot unload (uninstall) assemblies.
                    // The version of the existing (installed) assembly is greater or equal than the one, we try to install.
                    //if (existingInstalled.version >= assembly.AssemblyVersion)
                    //{
                    //    if (!existingInstalled.isComponentAssembly && assembly.IsComponentAssembly)
                    //    {
                    //        registerAsComponentAssembly.Add(assembly.AssemblyName);
                    //        installedAssemblies[assembly.AssemblyName] = (existingInstalled.version, isComponentAssembly: true, existingInstalled.module);
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
                            _logger?.LogWarning(
                                $"Cannnot install assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule} " +
                                $"as a minor version of the assembly is already installed by the host app, that cannot be unloaded. Ignoring the failure in good hope.");

                            if (!existingInstalled.isComponentAssembly && assembly.IsComponentAssembly)
                            {
                                registerAsComponentAssembly.Add(assembly.AssemblyName);
                                installedAssemblies[assembly.AssemblyName] = (existingInstalled.version, isComponentAssembly: true, existing.module);
                            }

                            reload = false;
                            break;
                        }
                    }

                    if (reload)
                    {
                        _logger?.LogWarning(
                            $"Cannnot install assembly {assembly.AssemblyName} {assembly.AssemblyVersion} as required by module {installedModule} " +
                            $"as a minor version of the assembly is already installed that cannot be unloaded. " +
                            $"We are reloading now.");

                        ReloadBrowser();
                        Environment.Exit(0);
                    }
                }
            }

            // Download and install all new assemblies.
            foreach (var assembly in installedAssemblies)
            {
                if (_installedAssemblies.ContainsKey(assembly.Key))
                {
                    continue;
                }

                await InstallAssemblyAsync(assembly, cancellation);
            }

            foreach (var asmName in registerAsComponentAssembly)
            {
                _logger?.LogDebug($"Installing {asmName} as app part.");

                var asm = _moduleAssemblyDownloader.GetAssembly(asmName);

                Assert(asm != null);

                if (asm != null)
                {
                    _assemblyManager.AddAssembly(asm);
                }
            }

            _installedAssemblies = installedAssemblies.ToImmutable();
        }

        private async ValueTask InstallAssemblyAsync(KeyValuePair<string, (Version version, bool isComponentAssembly, ModuleIdentifier module)> assembly, CancellationToken cancellation)
        {
            Assembly asm;
            do
            {
                asm = await _moduleAssemblyDownloader.InstallAssemblyAsync(assembly.Value.module, assembly.Key, cancellation);
            }
            while (asm == null); // TODO: Should we throw an exception and abort instead of retrying this forever?
        }

        private async ValueTask<BlazorModuleManifest> LoadManifestAsync(ModuleIdentifier installedModule, CancellationToken cancellation)
        {
            BlazorModuleManifest manifest;

            //do
            //{
            manifest = await _moduleManifestProvider.GetModuleManifestAsync(installedModule, cancellation);
            //}
            //while (manifest == null);  // TODO: Should we throw an exception and abort instead of retrying this forever?

            return manifest;
        }

        private void ReloadBrowser()
        {
            if (_jSRuntime is IJSInProcessRuntime jSInProcessRuntime)
            {
                jSInProcessRuntime.Invoke<object>(_reloadBrowserMethod);
            }
            else
            {
                _jSRuntime.InvokeAsync<object>(_reloadBrowserMethod).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        public void Dispose()
        {
            var cancellationSource = Interlocked.Exchange(ref _disposalCancellationSource, null);

            if (cancellationSource != null)
            {
                using (cancellationSource)
                {
                    cancellationSource.Cancel();

                    _runningModuleManager.ModuleStarted -= UpdateInstallation;
                    _runningModuleManager.ModuleTerminated -= UpdateInstallation;
                }
            }
        }
    }
}
