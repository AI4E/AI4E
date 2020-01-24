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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Utils;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#if !SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
using System.Runtime.Loader;
#endif

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleInstaller
    {
        private sealed class BlazorModule
        {
            public BlazorModule(
                BlazorModuleAssemblyLoadContext assemblyLoadContext,
                WeakReflectionContext reflectionContext,
                IChildServiceProvider moduleServices)
            {
                AssemblyLoadContext = assemblyLoadContext;
                ReflectionContext = reflectionContext;
                ModuleServices = moduleServices;
            }

            public WeakReflectionContext ReflectionContext { get; }
            public IChildServiceProvider ModuleServices { get; }
            public BlazorModuleAssemblyLoadContext AssemblyLoadContext { get; }
        }

        private readonly AssemblyManager _assemblyManager;
        private readonly IChildContainerBuilder _childContainerBuilder;
        private readonly IOptions<BlazorModuleOptions> _options;
        private readonly ILogger<BlazorModuleManager> _logger;
        private BlazorModule? _module = null;

        public BlazorModuleInstaller(
            IBlazorModuleDescriptor moduleDescriptor,
            AssemblyManager assemblyManager,
            IChildContainerBuilder childContainerBuilder,
            IOptions<BlazorModuleOptions> options,
            ILogger<BlazorModuleManager> logger)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (childContainerBuilder is null)
                throw new ArgumentNullException(nameof(childContainerBuilder));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            ModuleDescriptor = moduleDescriptor;
            _assemblyManager = assemblyManager;
            _childContainerBuilder = childContainerBuilder;
            _options = options;
            _logger = logger;
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
            var moduleTypeResolver = new ReflectionContextTypeResolver(
                        new BlazorModuleTypeResolver(assemblyLoadContext), reflectionContext);

            var moduleContext = new ModuleContext(ModuleDescriptor, assemblyLoadContext, reflectionContext, moduleTypeResolver);

            Assembly LoadAssembly(IBlazorModuleAssemblyDescriptor moduleAssemblyDescriptor)
            {
                return assemblyLoadContext.LoadFromAssemblyName(moduleAssemblyDescriptor.GetAssemblyName());
            }

            var componentAssemblies = ModuleDescriptor
                .Assemblies
                .Where(p => p.IsComponentAssembly)
                .Select(LoadAssembly)
                .ToImmutableList();

            void ConfigureServices(IServiceCollection services)
            {
                // Build and attach type-resolver             
                services.AddSingleton<ITypeResolver>(moduleTypeResolver);

                // Reset application services.
                services.AddSingleton(new ApplicationServiceManager());

                // Invoke module service configuration actions.
                var options = _options.Value;
                foreach (var configuration in options.ConfigureModuleServices)
                {
                    configuration(moduleContext, services);
                }

                // Add module context to services.
                services.AddSingleton(moduleContext);

                // Call the modules Startup.ConfigureServices method.
                var startupType = GetStartupType(new TypeResolver(componentAssemblies));

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

            var weakLoadContext = await UnloadModuleServicesAsync();

#if DEBUG

            // We are running on a sync-context.
            // Allow the renderer to re-render before disposing of module services that may be used by components.
            await Task.Yield();

            for (var i = 0; weakLoadContext.IsAlive && i < 100; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            if (weakLoadContext.IsAlive)
            {
                // This is here to enable tracking down unloadability problems via WINDBG/SOS
                Debugger.Break();

                throw new BlazorModuleManagerException($"Unable to unload module {ModuleDescriptor.Name}.");
            }

            Console.WriteLine($"Module {ModuleDescriptor.Name} unloaded successfully.");
#endif
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
                    var source = await moduleAssemblyDescriptor.LoadAssemblySourceAsync(cancellation);

                    try
                    {
                        result.Add(moduleAssemblyDescriptor.GetAssemblyName(), source);
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

#if DEBUG
        [MethodImpl(MethodImplOptions.NoInlining)]
#endif
        private async ValueTask<WeakReference> UnloadModuleServicesAsync()
        {
            Debug.Assert(_module != null);

            // Get all installed modules before! unloading 
            var assemblyLoadContext = _module!.AssemblyLoadContext;
            var installedAssemblies = assemblyLoadContext.InstalledAssemblies.ToImmutableHashSet(AssemblyByDisplayNameComparer.Instance);

            // Trigger alc unload
            assemblyLoadContext.Unload();

            // Remove the component assemblies (a subset of the installed assemblies) from the assembly manager 
            // and dispose all module services.
            await _assemblyManager.RemoveAssembliesAsync(installedAssemblies);
            await _module.ModuleServices.DisposeAsync();

            // Invoke registered cleanup actions
            var options = _options.Value;
            foreach (var cleanup in options.ConfigureCleanup)
            {
                await cleanup(installedAssemblies);
            }

            // Cleanup our datastructure to allow successfully gc'ing the alc
            _module = null;
            return new WeakReference(assemblyLoadContext);
        }
    }
}
