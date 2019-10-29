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
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleManager : IBlazorModuleManager, IAsyncDisposable
    {
        private readonly AssemblyManager _assemblyManager;
        private readonly IBlazorModuleAssemblyLoader _assemblyLoader;
        private readonly ILogger<BlazorModuleManager>? _logger;
        private readonly ImmutableDictionary<AssemblyName, Assembly> _coreAssemblies;

        // Contains all BlazorModule instances that are currently installed.
        // It has to be ensured that all installed modules are registered and no uninstalled modules are registered.
        private readonly Dictionary<BlazorModuleDescriptor, BlazorModule> _modules;

        private readonly AsyncDisposeHelper _disposeHelper;

        public BlazorModuleManager(
            AssemblyManager assemblyManager,
            IBlazorModuleAssemblyLoader assemblyLoader,
            ILogger<BlazorModuleManager>? logger = null)
        {
            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (assemblyLoader is null)
                throw new ArgumentNullException(nameof(assemblyLoader));

            _assemblyManager = assemblyManager;
            _assemblyLoader = assemblyLoader;
            _logger = logger;

            _coreAssemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .ToImmutableDictionary(p => p.GetName(), AssemblyNameComparer.Instance);

            _modules = new Dictionary<BlazorModuleDescriptor, BlazorModule>();
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Default);
        }

        public ValueTask<bool> InstallAsync(
            BlazorModuleDescriptor moduleDescriptor,
            CancellationToken cancellation)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (_modules.ContainsKey(moduleDescriptor))
            {
                return new ValueTask<bool>(false);
            }

            var blazorModule = new BlazorModule(moduleDescriptor, _coreAssemblies, _assemblyManager, _assemblyLoader);
            return InstallAsync(blazorModule, cancellation);
        }

        private async ValueTask<bool> InstallAsync(
            BlazorModule installedModule,
            CancellationToken cancellation)
        {
            await installedModule.InstallAsync(cancellation).ConfigureAwait(false);
            _modules[installedModule.ModuleDescriptor] = installedModule;
            return true;
        }

        public ValueTask<bool> UninstallAsync(
            BlazorModuleDescriptor moduleDescriptor,
            CancellationToken cancellation = default)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (!_modules.TryGetValue(moduleDescriptor, out var installedModule))
            {
                return new ValueTask<bool>(false);
            }

            return UninstallAsync(installedModule);
        }

        private async ValueTask<bool> UninstallAsync(BlazorModule installedModule)
        {
            await installedModule
                .UninstallAsync()
                .ConfigureAwait(false);

            _modules.Remove(installedModule.ModuleDescriptor);
            return true;
        }

        public bool IsInstalled(BlazorModuleDescriptor moduleDescriptor)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            return _modules.ContainsKey(moduleDescriptor);
        }

        public IEnumerable<BlazorModuleDescriptor> InstalledModules => _modules.Keys.ToImmutableList();

        #region Disposal

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private ValueTask DisposeInternalAsync()
        {
            return _modules.Values
                .Select(p => p.UninstallAsync())
                .WhenAll();
        }

        #endregion
    }
}
