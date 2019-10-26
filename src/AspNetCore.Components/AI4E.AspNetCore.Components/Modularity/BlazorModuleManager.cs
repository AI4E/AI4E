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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Utils.Async;
using AI4E.Utils.Memory;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleManager : IBlazorModuleManager, IAsyncDisposable
    {
        private readonly AssemblyManager _assemblyManager;
        private readonly IBlazorModuleAssemblyLoader _moduleAssemblyLoader;
        private readonly ILogger<BlazorModuleManager>? _logger;
        private readonly ImmutableDictionary<AssemblyName, Assembly> _coreAssemblies;

        // Contains all BlazorModuleInstallation instances that are currently installed.
        // It has to be ensured that all installed modules are registered and no uninstalled modules are registered.
        private readonly Dictionary<BlazorModuleDescriptor, BlazorModuleInstallation> _installedModules;

        private readonly AsyncDisposeHelper _disposeHelper;

        public BlazorModuleManager(
            AssemblyManager assemblyManager,
            IBlazorModuleAssemblyLoader moduleAssemblyLoader,
            ILogger<BlazorModuleManager>? logger = null)
        {
            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (moduleAssemblyLoader is null)
                throw new ArgumentNullException(nameof(moduleAssemblyLoader));

            _assemblyManager = assemblyManager;
            _moduleAssemblyLoader = moduleAssemblyLoader;
            _logger = logger;

            _coreAssemblies = AppDomain.CurrentDomain
                .GetAssemblies()
                .ToImmutableDictionary(p => p.GetName(), AssemblyNameComparer.Instance);

            _installedModules = new Dictionary<BlazorModuleDescriptor, BlazorModuleInstallation>();
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

            if (_installedModules.ContainsKey(moduleDescriptor))
            {
                return new ValueTask<bool>(false);
            }

            return InstallAsync(new BlazorModuleInstallation(moduleDescriptor, this), cancellation);
        }

        private async ValueTask<bool> InstallAsync(
            BlazorModuleInstallation installation,
            CancellationToken cancellation)
        {
            await installation.InstallAsync(cancellation).ConfigureAwait(false);
            _installedModules[installation.ModuleDescriptor] = installation;
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

            if (!_installedModules.TryGetValue(moduleDescriptor, out var installation))
            {
                return new ValueTask<bool>(false);
            }

            return UninstallAsync(installation);
        }

        private async ValueTask<bool> UninstallAsync(BlazorModuleInstallation installation)
        {
            await installation
                .UninstallAsync()
                .ConfigureAwait(false);

            _installedModules.Remove(installation.ModuleDescriptor);
            return true;
        }

        public bool IsInstalled(BlazorModuleDescriptor moduleDescriptor)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            return _installedModules.ContainsKey(moduleDescriptor);
        }

        public IEnumerable<BlazorModuleDescriptor> InstalledModules => _installedModules.Keys.ToImmutableList();

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
            return _installedModules.Values
                .Select(p => p.UninstallAsync())
                .WhenAll();
        }

        #endregion

        private sealed class BlazorModuleInstallation
        {
            private readonly BlazorModuleManager _manager;
            private Dictionary<AssemblyName, Assembly>? _assemblyCache;
            private ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource>? _assemblySources;
            private BlazorModuleAssemblyLoadContext? _assemblyLoadContext;
            private ImmutableList<Assembly>? _installedAssemblies;

            public BlazorModuleInstallation(BlazorModuleDescriptor moduleDescriptor, BlazorModuleManager manager)
            {
                ModuleDescriptor = moduleDescriptor;
                _manager = manager;
            }

            public BlazorModuleDescriptor ModuleDescriptor { get; }

            public async ValueTask InstallAsync(CancellationToken cancellation)
            {
                if (IsInstalled)
                {
                    return;
                }

                _assemblyCache = new Dictionary<AssemblyName, Assembly>(AssemblyNameComparer.Instance);
                _assemblySources = await PrefetchAssemblySourcesAsync(ModuleDescriptor.Assemblies, cancellation);
                _assemblyLoadContext = new BlazorModuleAssemblyLoadContext(
                    _manager._coreAssemblies, _assemblySources, _assemblyCache);
                _installedAssemblies = GetComponentAssemblies().ToImmutableList();

                await _manager._assemblyManager.AddAssembliesAsync(_installedAssemblies, _assemblyLoadContext);

                IsInstalled = true;
            }

            public async ValueTask UninstallAsync()
            {
                if (!IsInstalled)
                {
                    return;
                }

                await RemoveFromAssemblyManagerAsync();
                await Task.Yield(); // We are running on a sync-context. Allow the renderer to re-render.
                Unload(out var weakRef);

                for (var i = 0; weakRef.IsAlive && i < 100; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                if (weakRef.IsAlive)
                {
                    throw new Exception($"Unable to unload module {ModuleDescriptor.Name}."); // TODO: Exception type
                }

                IsInstalled = false;
            }


            private IEnumerable<Assembly> GetComponentAssemblies()
            {
                foreach (var assembly in ModuleDescriptor.Assemblies.Where(p => p.IsComponentAssembly))
                {
                    var assemblyName = new AssemblyName(assembly.AssemblyName) { Version = assembly.AssemblyVersion };

                    if (!_assemblyCache!.TryGetValue(assemblyName, out var asm))
                    {
                        var assemblySource = _assemblySources![assemblyName];
                        using var assemblyStream = new PooledMemoryStream(assemblySource.AssemblyBytes);

                        if (assemblySource.HasSymbols)
                        {
                            using var assemblySymbolsStream = new PooledMemoryStream(assemblySource.AssemblySymbolsBytes);
                            asm = _assemblyLoadContext!.LoadFromStream(assemblyStream, assemblySymbolsStream);
                        }
                        else
                        {
                            asm = _assemblyLoadContext!.LoadFromStream(assemblyStream);
                        }
                    }

                    yield return asm;
                }
            }

            private async ValueTask<ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource>> PrefetchAssemblySourcesAsync(
                ImmutableList<BlazorModuleAssemblyDescriptor> moduleAssemblyDescriptors,
                CancellationToken cancellation)
            {
                var result = ImmutableDictionary.CreateBuilder<AssemblyName, BlazorModuleAssemblySource>(AssemblyNameComparer.Instance);

                try
                {
                    foreach (var moduleAssemblyDescriptor in moduleAssemblyDescriptors)
                    {
                        var assemblyName = new AssemblyName(moduleAssemblyDescriptor.AssemblyName) 
                        { 
                            Version = moduleAssemblyDescriptor.AssemblyVersion 
                        };

                        if (_manager._coreAssemblies.ContainsKey(assemblyName))
                        {
                            continue;
                        }

                        var source = await LoadAssemblyAsync(assemblyName, cancellation);

                        try
                        {
                            result.Add(assemblyName, source);
                        }
                        catch
                        {
                            source.Dispose();
                            throw;
                        }
                    }
                }
                catch
                {
                    foreach (var source in result.Values)
                    {
                        source.Dispose();
                    }

                    throw;
                }

                return result.ToImmutable();
            }

            private ValueTask<BlazorModuleAssemblySource> LoadAssemblyAsync(AssemblyName assemblyName, CancellationToken cancellation)
            {
                return _manager._moduleAssemblyLoader.LoadAssemblySourceAsync(ModuleDescriptor, assemblyName, cancellation);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private async ValueTask RemoveFromAssemblyManagerAsync()
            {
                await _manager._assemblyManager.RemoveAssembliesAsync(_installedAssemblies!);
                RemoveFromInternalCaches(_installedAssemblies!.ToHashSet());
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void Unload(out WeakReference weakRef)
            {
                // TODO: Either remove the multi-targeting, or add a shim for this.
#if !SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
                throw new NotSupportedException("Uninstalling modules is not supported on this platform.");
#else
                _assemblyLoadContext!.Unload();
                weakRef = new WeakReference(_assemblyLoadContext);
                _assemblyCache = null;
                _assemblySources = null;
                _assemblyLoadContext = null;
                _installedAssemblies = null;
#endif
            }

            public bool IsInstalled { get; private set; } = false;

            #region Blazor caching workaround

            private static readonly Action<HashSet<Assembly>> RemoveFromAttributeAuthorizeDataCache = RemoveFromCache(
            "Microsoft.AspNetCore.Components.Authorization",
            "Microsoft.AspNetCore.Components.Authorization.AttributeAuthorizeDataCache",
            "_cache");

            private static readonly Action<HashSet<Assembly>> RemoveFromFormatterDelegateCache = RemoveFromCache(
                "Microsoft.AspNetCore.Components",
                "Microsoft.AspNetCore.Components.BindConverter+FormatterDelegateCache",
                "_cache");

            private static readonly Action<HashSet<Assembly>> RemoveFromParserDelegateCache = RemoveFromCache(
               "Microsoft.AspNetCore.Components",
               "Microsoft.AspNetCore.Components.BindConverter+ParserDelegateCache",
               "_cache");

            private static readonly Action<HashSet<Assembly>> RemoveFromCascadingParameterState = RemoveFromCache(
               "Microsoft.AspNetCore.Components",
               "Microsoft.AspNetCore.Components.CascadingParameterState",
               "_cachedInfos");

            private static readonly Action<HashSet<Assembly>> RemoveFromComponentFactory
                = BuildRemoveFromComponentFactory();

            private static readonly Action<HashSet<Assembly>> RemoveFromComponentProperties = RemoveFromCache(
             "Microsoft.AspNetCore.Components",
             "Microsoft.AspNetCore.Components.Reflection.ComponentProperties",
             "_cachedWritersByType");

            private static readonly Action<HashSet<Assembly>> RemoveFromInternalCaches =
                RemoveFromAttributeAuthorizeDataCache +
                RemoveFromFormatterDelegateCache +
                RemoveFromParserDelegateCache +
                RemoveFromCascadingParameterState +
                RemoveFromComponentFactory +
                RemoveFromComponentProperties;

            private static Action<HashSet<Assembly>> RemoveFromCache(
                string assembly,
                string typeName,
                string fieldName,
                object? instance = null)
            {
                var type = GetType(assembly, typeName);

                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic;

                if (instance is null)
                {
                    bindingFlags |= BindingFlags.Static;
                }
                else
                {
                    bindingFlags |= BindingFlags.Instance;
                }

                var field = type.GetField(fieldName, bindingFlags)
                    ?? throw new Exception($"Unable to reflect field '{fieldName}' of type '{type}'");

                if (!(field.GetValue(instance) is IDictionary cache))
                    return _ => { };

                void RemoveFromCache(HashSet<Assembly> unloaded)
                {
                    var typesToRemove = cache.Keys.OfType<Type>().Where(p => unloaded.Contains(p.Assembly)).ToList();

                    foreach (var type in typesToRemove)
                    {
                        cache.Remove(type);
                    }
                }

                return RemoveFromCache;
            }

            private static Action<HashSet<Assembly>> BuildRemoveFromComponentFactory()
            {
                var assembly = "Microsoft.AspNetCore.Components";
                var typeName = "Microsoft.AspNetCore.Components.ComponentFactory";
                var fieldName = "_cachedInitializers";

                var type = GetType(assembly, typeName);
                var instance = type.GetField(
                    "Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    !.GetValue(null);
                return RemoveFromCache(assembly, typeName, fieldName, instance);
            }

            private static Type GetType(string assembly, string typeName)
            {
                return Type.GetType($"{typeName}, {assembly}")
                ?? throw new Exception($"Unable to reflect type '{typeName}, {assembly}'.");
            }

            #endregion
        }

        private sealed class BlazorModuleAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly ImmutableDictionary<AssemblyName, Assembly> _coreAssemblies;
            private readonly ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> _assemblySources;
            private readonly Dictionary<AssemblyName, Assembly> _assemblyCache;

            public BlazorModuleAssemblyLoadContext(
                ImmutableDictionary<AssemblyName, Assembly> coreAssemblies,
                ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> assemblySources,
                Dictionary<AssemblyName, Assembly> assemblyCache)

            // TODO: Either remove the multi-targeting, or add a shim for this.
#if SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
                : base(isCollectible: true)
#endif
            {
                if (coreAssemblies is null)
                    throw new ArgumentNullException(nameof(coreAssemblies));

                if (assemblyCache is null)
                    throw new ArgumentNullException(nameof(assemblyCache));

                _coreAssemblies = coreAssemblies;
                _assemblySources = assemblySources;
                _assemblyCache = assemblyCache;
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                if (_coreAssemblies.TryGetValue(assemblyName, out var assembly))
                {
                    return assembly;
                }

                if (_assemblyCache.TryGetValue(assemblyName, out assembly!))
                {
                    return assembly;
                }

                if (_assemblySources.TryGetValue(assemblyName, out var assemblySource))
                {
                    using var assemblyStream = new PooledMemoryStream(assemblySource.AssemblyBytes);

                    if (assemblySource.HasSymbols)
                    {
                        using var assemblySymbolsStream = new PooledMemoryStream(assemblySource.AssemblySymbolsBytes);
                        assembly = LoadFromStream(assemblyStream, assemblySymbolsStream);
                    }
                    else
                    {
                        assembly = LoadFromStream(assemblyStream);
                    }

                    _assemblyCache.Add(assemblyName, assembly);
                    return assembly;
                }

                return null;
            }
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static AssemblyNameComparer Instance { get; } = new AssemblyNameComparer();

            private AssemblyNameComparer() { }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                return string.Equals(x?.FullName, y?.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(AssemblyName obj)
            {
                return obj.FullName.GetHashCode(StringComparison.Ordinal);
            }
        }
    }
}
