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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleRunner : IAsyncDisposable, IDisposable
    {
        #region Fields

        private readonly IBlazorModuleSource _moduleSource;
        private readonly IBlazorModuleManager _moduleManager;
        private readonly ILogger<BlazorModuleRunner>? _logger;

        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        // Initially, we are in the process (See InitInternalAsync)
        private bool _inProcess = true;
        private bool _needsUpdate = false;
        private readonly object _mutex = new object();

        #endregion

        #region C'tor

        public BlazorModuleRunner(
            IBlazorModuleSourceFactory moduleSourceFactory,
            IBlazorModuleManager moduleManager,
            IOptions<BlazorModuleOptions> options,
            ILogger<BlazorModuleRunner>? logger)
        {
            if (moduleSourceFactory is null)
                throw new ArgumentNullException(nameof(moduleSourceFactory));

            if (moduleManager is null)
                throw new ArgumentNullException(nameof(moduleManager));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            var moduleSource = moduleSourceFactory.CreateModuleSource();

            foreach(var moduleSourceConfiguration in options.Value.ConfigureModuleSource)
            {
                moduleSource = moduleSourceConfiguration(moduleSource);
            }

            _moduleSource = moduleSource;
            _moduleManager = moduleManager;
            _logger = logger;

            _moduleSource.ModulesChanged += ModulesChanged;

            _initializationHelper = new AsyncInitializationHelper(InitInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Default);
        }

        #endregion

        #region Initialization

        private Task Initialization => _initializationHelper.Initialization;

        private Task InitInternalAsync(CancellationToken cancellation)
        {
            return UpdateAsync(initial: true);
        }

        #endregion

        #region Disposal

        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        private async ValueTask DisposeInternalAsync()
        {
            await _initializationHelper.CancelAsync().ConfigureAwait(false);

            _moduleSource.ModulesChanged -= ModulesChanged;
        }

        #endregion

        private void ModulesChanged(object? sender, EventArgs e)
        {
            UpdateAsync().HandleExceptions(_logger);
        }

        private async Task UpdateAsync(bool initial = false)
        {
            if (!initial)
            {
                lock (_mutex)
                {
                    if (_inProcess)
                    {
                        _needsUpdate = true;
                        return;
                    }

                    _inProcess = true;
                    _needsUpdate = false;
                }
            }

            var needsUpdate = true;

            while (needsUpdate)
            {
                try
                {
                    await ProcessAsync(_disposeHelper.AsCancellationToken()).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    return;
                }
                finally
                {
                    lock (_mutex)
                    {
                        needsUpdate = _needsUpdate;
                        _needsUpdate = false;
                        if (!needsUpdate)
                        {
                            _inProcess = false;
                        }
                    }
                }
            }
        }

        private async Task ProcessAsync(CancellationToken cancellation)
        {
            var modules = _moduleSource.GetModulesAsync(cancellation);
            var installedModules = _moduleManager.InstalledModules.ToAsyncEnumerable();

            var toBeUninstalled = installedModules.Except(modules);
            var toBeInstalled = modules.Except(installedModules);

            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                Debug.Assert(_logger != null);

                static async ValueTask<string> FormatAsync(IAsyncEnumerable<IBlazorModuleDescriptor> modules)
                {
                    StringBuilder? result = null;
                    await foreach (var module in modules)
                    {
                        if (result is null)
                        {
                            result = new StringBuilder(module.Name);
                        }
                        else
                        {
                            result.Append(',');
                            result.Append(' ');
                            result.Append(module.Name);
                        }
                    }

                    if (result is null)
                        return string.Empty;

                    return result.ToString();
                }

                _logger.LogTrace("Desired installation set: " + await FormatAsync(modules));
                _logger.LogTrace("Current installation set: " + await FormatAsync(installedModules));
                _logger.LogTrace("To be uninstalled modules: " + await FormatAsync(toBeUninstalled));
                _logger.LogTrace("To be installed modules: " + await FormatAsync(toBeInstalled));
            }

            // Do NOT parallelize this. IModuleManager instances are NOT guaranteed to be thread-safe.
            await foreach (var module in toBeUninstalled)
            {
                cancellation.ThrowIfCancellationRequested();

                await _moduleManager.UninstallAsync(module, cancellation).ConfigureAwait(false);

                var needsUpdate = Volatile.Read(ref _needsUpdate);

                if (needsUpdate)
                {
                    return;
                }
            }

            await foreach (var module in toBeInstalled)
            {
                cancellation.ThrowIfCancellationRequested();

                await _moduleManager.InstallAsync(module, cancellation).ConfigureAwait(false);

                var needsUpdate = Volatile.Read(ref _needsUpdate);

                if (needsUpdate)
                {
                    return;
                }
            }
        }

        public static void Configure(IServiceCollection services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            services.AddSingleton<BlazorModuleRunner>();
            services.ConfigureApplicationServices(ConfigureApplicationServices);
        }

        private static void ConfigureApplicationServices(ApplicationServiceManager serviceManager)
        {
            static Task ModuleRunnerInitialization(BlazorModuleRunner moduleRunner, IServiceProvider ServiceProvider)
            {
                return moduleRunner.Initialization;
            }

            // Ensure that the module runner is initialized at application startup.
            serviceManager.AddService((Func<BlazorModuleRunner, IServiceProvider, Task>)ModuleRunnerInitialization, isRequiredService: true);
        }
    }
}
