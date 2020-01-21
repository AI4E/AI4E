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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Utils;
using AI4E.Utils.ApplicationParts;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleInstaller
    {
        private sealed class BlazorModule
        {
            public BlazorModule(
                BlazorModuleAssemblyLoadContext assemblyLoadContext,
                WeakReflectionContext reflectionContext,
                ImmutableList<Assembly> componentAssemblies,
                IChildServiceProvider moduleServices)
            {
                AssemblyLoadContext = assemblyLoadContext;
                ReflectionContext = reflectionContext;
                ComponentAssemblies = componentAssemblies;
                //InstalledAssemblies = installedAssemblies;
                ModuleServices = moduleServices;
            }

            public WeakReflectionContext ReflectionContext { get; }
            public ImmutableList<Assembly> ComponentAssemblies { get; }
            public IChildServiceProvider ModuleServices { get; }
            public BlazorModuleAssemblyLoadContext AssemblyLoadContext { get; }
        }

        private readonly AssemblyManager _assemblyManager;
        private readonly IChildContainerBuilder _childContainerBuilder;

        private BlazorModule? _module = null;

        public BlazorModuleInstaller(
            IBlazorModuleDescriptor moduleDescriptor,
            AssemblyManager assemblyManager,
            IChildContainerBuilder childContainerBuilder)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (childContainerBuilder is null)
                throw new ArgumentNullException(nameof(childContainerBuilder));

            ModuleDescriptor = moduleDescriptor;
            _assemblyManager = assemblyManager;
            _childContainerBuilder = childContainerBuilder;
        }

        public IBlazorModuleDescriptor ModuleDescriptor { get; }

        public bool IsInstalled => _module != null;

        public async ValueTask InstallAsync(CancellationToken cancellation)
        {
            if (IsInstalled)
            {
                return;
            }

            var assemblySources = await PrefetchAssemblySourcesAsync(ModuleDescriptor.Assemblies, cancellation);
            var assemblyLoadContext = new BlazorModuleAssemblyLoadContext(assemblySources);
            var reflectionContext = new WeakReflectionContext();

            IEnumerable<Assembly> GetComponentAssemblies()
            {
                return ModuleDescriptor.Assemblies
                    .Where(p => p.IsComponentAssembly)
                    .Select(GetComponentAssembly);
            }

            IEnumerable<Assembly> GetInstalledAssemblies()
            {
                return ModuleDescriptor.Assemblies
                    .Select(GetComponentAssembly);
            }

            Assembly GetComponentAssembly(IBlazorModuleAssemblyDescriptor moduleAssemblyDescriptor)
            {
                var assemblyName = new AssemblyName(moduleAssemblyDescriptor.AssemblyName)
                {
                    Version = moduleAssemblyDescriptor.AssemblyVersion
                };

                return assemblyLoadContext.LoadFromAssemblyName(assemblyName);
            }

            var installedAssemblies = GetInstalledAssemblies().ToImmutableList();
            var componentAssemblies = GetComponentAssemblies().ToImmutableList();

            void ConfigureServices(IServiceCollection services)
            {
                // Build and attach type-resolver
                var typeResolver = new TypeResolver(installedAssemblies);
                services.AddSingleton<ITypeResolver>(
                    new ReflectionContextTypeResolver(typeResolver, reflectionContext));

                // Reset application services.
                services.AddSingleton(new ApplicationServiceManager());

                // Reset pplication parts and add installed assemblies by default.
                var partManager = new ApplicationPartManager();
                partManager.ApplicationParts.Clear();
                foreach (var componentAssembly in componentAssemblies)
                {
                    partManager.ApplicationParts.Add(new AssemblyPart(componentAssembly));
                }

                services.AddSingleton(partManager);

                // TODO: Add an extensibility point to allow custom service reset actions.

                // Call the modules Startup.ConfigureServices method
                var startupType = GetStartupType(typeResolver);

                if (startupType != null)
                {
                    var hostingServiceProvider = services.BuildServiceProvider();

                    IBlazorModuleStartup startup;

                    if (typeof(IBlazorModuleStartup).IsAssignableFrom(startupType))
                    {
                        startup = (IBlazorModuleStartup)ActivatorUtilities.GetServiceOrCreateInstance(
                            hostingServiceProvider, startupType);
                    }
                    else
                    {
                        startup = new ConventionBasedBlazorModuleStartup(
                            hostingServiceProvider, startupType, string.Empty); // TODO: Specify environment name
                    }

                    // TODO: We currently do not support custom service providers. We can strip the support for this in
                    //       * ConventionBasedBlazorModuleStartup
                    //       * IBlazorModuleStartup
                    //       * ConfigureServicesBuilder
                    startup.ConfigureServices(services);
                }
            }

            var moduleServices = _childContainerBuilder.CreateChildContainer(ConfigureServices);
            var appServices = moduleServices.GetService<ApplicationServiceManager>();

            if (appServices != null)
            {
                await appServices
                    .InitializeApplicationServicesAsync(moduleServices, cancellation)
                    .ConfigureAwait(false);
            }

            await _assemblyManager.AddAssembliesAsync(
                componentAssemblies.Select(p => reflectionContext.MapAssembly(p)),
                assemblyLoadContext,
                new WeakServiceProvider(moduleServices));

            _module = new BlazorModule(
                assemblyLoadContext,
                reflectionContext,
                componentAssemblies,
                //installedAssemblies,
                moduleServices);
        }

        private Type? GetStartupType(ITypeResolver typeResolver)
        {
            if (ModuleDescriptor.StartupType is null)
                return null;

            if (ModuleDescriptor.StartupType.Value.TryGetType(out var result))
                return result;

            if (typeResolver.TryResolveType(ModuleDescriptor.StartupType.Value.TypeName.AsSpan(), out result))
                return result;

            return null;
        }

        public async ValueTask UninstallAsync()
        {
            if (!IsInstalled)
            {
                return;
            }

            await UnloadModuleServicesAsync();
            await Task.Yield(); // We are running on a sync-context. Allow the renderer to re-render.
            Unload(out var weakLoadContext);

            for (var i = 0; weakLoadContext.IsAlive && i < 100; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (weakLoadContext.IsAlive)
            {
#if DEBUG
                // This is here to enable tracking down unloadability problems via WINDBG/SOS
                Debugger.Break();
#endif
                throw new BlazorModuleManagerException($"Unable to unload module {ModuleDescriptor.Name}.");
            }

            Console.WriteLine($"Module {ModuleDescriptor.Name} unloaded successfully.");
        }

        private async ValueTask<ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource>> PrefetchAssemblySourcesAsync(
            ImmutableList<IBlazorModuleAssemblyDescriptor> moduleAssemblyDescriptors,
            CancellationToken cancellation)
        {
            var result = ImmutableDictionary.CreateBuilder<AssemblyName, BlazorModuleAssemblySource>(AssemblyNameComparer.ByDisplayName);

            try
            {
                foreach (var moduleAssemblyDescriptor in moduleAssemblyDescriptors)
                {
                    var assemblyName = new AssemblyName(moduleAssemblyDescriptor.AssemblyName)
                    {
                        Version = moduleAssemblyDescriptor.AssemblyVersion
                    };

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
        private async ValueTask UnloadModuleServicesAsync()
        {
            Debug.Assert(_module != null);
            var assemblyLoadContext = _module!.AssemblyLoadContext;

            // TODO: Either remove the multi-targeting, or add a shim for this.
#if !SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
            throw new NotSupportedException("Uninstalling modules is not supported on this platform.");
#else
            // TODO: Move the unload into UnloadModuleServicesAsync() to prevent any new assemblies beeing loaded from the ALC while unloading (sealing it)
            assemblyLoadContext!.Unload();
#endif

            await _assemblyManager.RemoveAssembliesAsync(_module!.ComponentAssemblies);

            var installedAssemblies = assemblyLoadContext.Assemblies.ToImmutableHashSet(AssemblyByDisplayNameComparer.Instance);

            // TODO: Add an extensibility point to allow custom unload actions.

            RemoveFromInternalCaches(installedAssemblies);
            RemoveFromAutofac(installedAssemblies);

            await _module!.ModuleServices.DisposeAsync();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Unload(out WeakReference weakRef)
        {
            Debug.Assert(_module != null);

            var assemblyLoadContext = _module!.AssemblyLoadContext;
            weakRef = new WeakReference(assemblyLoadContext);
            _module = null;
        }

        #region Autofac caching workaround

        private static void RemoveFromAutofac(ImmutableHashSet<Assembly> unloaded)
        {
            var autofac = Assembly.Load("Autofac");

            void RemoveFromConstructorParameterBinding(ImmutableHashSet<Assembly> unloaded)
            {
                var constructorParameterBindingType = autofac.GetType("Autofac.Core.Activators.Reflection.ConstructorParameterBinding");
                var constructorInvokers = (IDictionary)constructorParameterBindingType!.GetField("ConstructorInvokers", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var keysToRemove = constructorInvokers.Keys
                    .OfType<ConstructorInfo>()
                    .Where(p => unloaded.Contains(p.DeclaringType!.Assembly))
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    constructorInvokers.Remove(key);
                }
            }

            void RemoveFromAutowiringPropertyInjector(ImmutableHashSet<Assembly> unloaded)
            {
                var autowiringPropertyInjectorType = autofac.GetType("Autofac.Core.Activators.Reflection.AutowiringPropertyInjector");
                var propertySetters = (IDictionary)autowiringPropertyInjectorType!.GetField("PropertySetters", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var propertySettersKeysToRemove = propertySetters.Keys
                    .OfType<PropertyInfo>()
                    .Where(p => unloaded.Contains(p.DeclaringType!.Assembly))
                    .ToList();

                foreach (var key in propertySettersKeysToRemove)
                {
                    propertySetters.Remove(key);
                }

                var injectableProperties = (IDictionary)autowiringPropertyInjectorType!.GetField("InjectableProperties", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var injectablePropertiesKeysToRemove = injectableProperties.Keys
                    .OfType<Type>()
                    .Where(p => unloaded.Contains(p.Assembly))
                    .ToList();

                foreach (var key in injectablePropertiesKeysToRemove)
                {
                    injectableProperties.Remove(key);
                }
            }

            void RemoveFromDefaultConstructorFinder(ImmutableHashSet<Assembly> unloaded)
            {
                var defaultConstructorFinderType = autofac.GetType("Autofac.Core.Activators.Reflection.DefaultConstructorFinder");
                var defaultPublicConstructorsCache = (IDictionary)defaultConstructorFinderType!.GetField("DefaultPublicConstructorsCache", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var defaultPublicConstructorsCacheKeysToRemove = defaultPublicConstructorsCache.Keys
                    .OfType<Type>()
                    .Where(p => unloaded.Contains(p.Assembly))
                    .ToList();

                foreach (var key in defaultPublicConstructorsCacheKeysToRemove)
                {
                    defaultPublicConstructorsCache.Remove(key);
                }
            }

            RemoveFromConstructorParameterBinding(unloaded);
            RemoveFromAutowiringPropertyInjector(unloaded);
            RemoveFromDefaultConstructorFinder(unloaded);
        }

        #endregion

        #region Blazor caching workaround

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromAttributeAuthorizeDataCache = RemoveFromCache(
        "Microsoft.AspNetCore.Components.Authorization",
        "Microsoft.AspNetCore.Components.Authorization.AttributeAuthorizeDataCache",
        "_cache");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromFormatterDelegateCache = RemoveFromCache(
            "Microsoft.AspNetCore.Components",
            "Microsoft.AspNetCore.Components.BindConverter+FormatterDelegateCache",
            "_cache");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromParserDelegateCache = RemoveFromCache(
           "Microsoft.AspNetCore.Components",
           "Microsoft.AspNetCore.Components.BindConverter+ParserDelegateCache",
           "_cache");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromCascadingParameterState = RemoveFromCache(
           "Microsoft.AspNetCore.Components",
           "Microsoft.AspNetCore.Components.CascadingParameterState",
           "_cachedInfos");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromComponentFactory
            = BuildRemoveFromComponentFactory();

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromComponentProperties = RemoveFromCache(
         "Microsoft.AspNetCore.Components",
         "Microsoft.AspNetCore.Components.Reflection.ComponentProperties",
         "_cachedWritersByType");

        private static readonly Action<ImmutableHashSet<Assembly>> RemoveFromInternalCaches =
            RemoveFromAttributeAuthorizeDataCache +
            RemoveFromFormatterDelegateCache +
            RemoveFromParserDelegateCache +
            RemoveFromCascadingParameterState +
            RemoveFromComponentFactory +
            RemoveFromComponentProperties;

        private static Action<ImmutableHashSet<Assembly>> RemoveFromCache(
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

            void RemoveFromCache(ImmutableHashSet<Assembly> unloaded)
            {
                var typesToRemove = cache.Keys
                    .OfType<Type>()
                    .Where(p => unloaded.Contains(p.Assembly))
                    .ToList();

                foreach (var type in typesToRemove)
                {
                    cache.Remove(type);
                }
            }

            return RemoveFromCache;
        }

        private static Action<ImmutableHashSet<Assembly>> BuildRemoveFromComponentFactory()
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
