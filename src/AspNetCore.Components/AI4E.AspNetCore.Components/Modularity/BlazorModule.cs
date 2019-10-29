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
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModule
    {
        private readonly ImmutableDictionary<AssemblyName, Assembly> _coreAssemblies;
        private readonly AssemblyManager _assemblyManager;

        private BlazorModuleAssemblyLoadContext? _assemblyLoadContext;
        private ImmutableList<Assembly>? _installedAssemblies;

        public BlazorModule(
            IBlazorModuleDescriptor moduleDescriptor,
            ImmutableDictionary<AssemblyName, Assembly> coreAssemblies,
            AssemblyManager assemblyManager)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (coreAssemblies is null)
                throw new ArgumentNullException(nameof(coreAssemblies));

            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            ModuleDescriptor = moduleDescriptor;
            _coreAssemblies = coreAssemblies;
            _assemblyManager = assemblyManager;
        }

        public IBlazorModuleDescriptor ModuleDescriptor { get; }

        public async ValueTask InstallAsync(CancellationToken cancellation)
        {
            if (IsInstalled)
            {
                return;
            }

            var assemblySources = await PrefetchAssemblySourcesAsync(ModuleDescriptor.Assemblies, cancellation);
            _assemblyLoadContext = new BlazorModuleAssemblyLoadContext(_coreAssemblies, assemblySources);
            _installedAssemblies = GetComponentAssemblies().ToImmutableList();

            await _assemblyManager.AddAssembliesAsync(_installedAssemblies, _assemblyLoadContext);

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
            Unload(out var weakLoadContext);

            for (var i = 0; weakLoadContext.IsAlive && i < 100; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (weakLoadContext.IsAlive)
            {
                throw new Exception($"Unable to unload module {ModuleDescriptor.Name}."); // TODO: Exception type
            }

            IsInstalled = false;
        }

        private IEnumerable<Assembly> GetComponentAssemblies()
        {
            return ModuleDescriptor.Assemblies.Where(p => p.IsComponentAssembly).Select(GetComponentAssembly);
        }

        private Assembly GetComponentAssembly(IBlazorModuleAssemblyDescriptor moduleAssemblyDescriptor)
        {
            var assemblyName = new AssemblyName(moduleAssemblyDescriptor.AssemblyName)
            {
                Version = moduleAssemblyDescriptor.AssemblyVersion
            };

            return _assemblyLoadContext!.LoadFromAssemblyName(assemblyName);
        }

        private async ValueTask<ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource>> PrefetchAssemblySourcesAsync(
            ImmutableList<IBlazorModuleAssemblyDescriptor> moduleAssemblyDescriptors,
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

                    if (_coreAssemblies.ContainsKey(assemblyName))
                    {
                        continue;
                    }

                    var source = await moduleAssemblyDescriptor.LoadAssemblySourceAsync(cancellation);

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask RemoveFromAssemblyManagerAsync()
        {
            await _assemblyManager.RemoveAssembliesAsync(_installedAssemblies!);
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
}
