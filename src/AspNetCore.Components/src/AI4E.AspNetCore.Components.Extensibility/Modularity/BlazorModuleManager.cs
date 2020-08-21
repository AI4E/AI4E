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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace AI4E.AspNetCore.Components.Modularity
{
    /// <summary>
    /// Manages the installation of blazor modules.
    /// </summary>
    /// <remarks>
    /// This type is not thread-safe.
    /// </remarks>
    public sealed class BlazorModuleManager : IBlazorModuleManager, IAsyncDisposable
    {
        private readonly IAssemblyRegistry _assemblyManager;
        private readonly IChildContainerBuilder _childContainerBuilder;
        private readonly IOptions<BlazorModuleOptions> _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<BlazorModuleManager> _logger;

        // Contains all BlazorModule instances that are currently installed.
        // It has to be ensured that all installed modules are registered and no uninstalled modules are registered.
        private readonly Dictionary<IBlazorModuleDescriptor, BlazorModuleInstaller> _moduleInstallers;

        private readonly AsyncDisposeHelper _disposeHelper;

        public BlazorModuleManager(
            IAssemblyRegistry assemblyManager,
            IChildContainerBuilder childContainerBuilder,
            IOptions<BlazorModuleOptions> options,
            ILoggerFactory? loggerFactory = null)
        {
            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (childContainerBuilder is null)
                throw new ArgumentNullException(nameof(childContainerBuilder));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _assemblyManager = assemblyManager;
            _childContainerBuilder = childContainerBuilder;
            _options = options;
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<BlazorModuleManager>();

            _moduleInstallers = new Dictionary<IBlazorModuleDescriptor, BlazorModuleInstaller>();
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Default);
        }

        public ValueTask<bool> InstallAsync(
            IBlazorModuleDescriptor moduleDescriptor,
            CancellationToken cancellation)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (_moduleInstallers.ContainsKey(moduleDescriptor))
            {
                return new ValueTask<bool>(false);
            }

            var blazorModule = new BlazorModuleInstaller(
                moduleDescriptor,
                _assemblyManager,
                _childContainerBuilder,
                _options,
                _loggerFactory);

            return InstallAsync(blazorModule, cancellation);
        }

        private async ValueTask<bool> InstallAsync(
            BlazorModuleInstaller installedModule,
            CancellationToken cancellation)
        {
            await installedModule.InstallAsync(cancellation).ConfigureAwait(false);
            _moduleInstallers[installedModule.ModuleDescriptor] = installedModule;
            return true;
        }

        public ValueTask<bool> UninstallAsync(
            IBlazorModuleDescriptor moduleDescriptor,
            CancellationToken cancellation = default)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            if (!_moduleInstallers.TryGetValue(moduleDescriptor, out var installedModule))
            {
                return new ValueTask<bool>(false);
            }

            return UninstallAsync(installedModule);
        }

        private async ValueTask<bool> UninstallAsync(BlazorModuleInstaller installedModule)
        {
            await installedModule
                .UninstallAsync()
                .ConfigureAwait(false);

            _moduleInstallers.Remove(installedModule.ModuleDescriptor);
            return true;
        }

        public bool IsInstalled(IBlazorModuleDescriptor moduleDescriptor)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            return _moduleInstallers.ContainsKey(moduleDescriptor);
        }

        public IEnumerable<IBlazorModuleDescriptor> InstalledModules => _moduleInstallers.Keys.ToImmutableList();

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
            return _moduleInstallers.Values
                .Select(p => p.UninstallAsync())
                .WhenAll();
        }

        #endregion
    }
}
