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
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Extensibility;
using AI4E.Utils;
using AI4E.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleInstaller
    {
        private sealed class BlazorModule
        {
            public BlazorModule(
                AssemblyLoadContext assemblyLoadContext,
                ReflectionContext reflectionContext,
                IChildServiceProvider moduleServices)
            {
                AssemblyLoadContext = assemblyLoadContext;
                ReflectionContext = reflectionContext;
                ModuleServices = moduleServices;
            }

            public ReflectionContext ReflectionContext { get; }
            public IChildServiceProvider ModuleServices { get; }
            public AssemblyLoadContext AssemblyLoadContext { get; }
        }

        private readonly IAssemblyRegistry _assemblyManager;
        private readonly IChildContainerBuilder _childContainerBuilder;
        private readonly IOptions<BlazorModuleOptions> _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<BlazorModuleInstaller> _logger;
        private BlazorModule? _module = null;

        public BlazorModuleInstaller(
            IBlazorModuleDescriptor moduleDescriptor,
            IAssemblyRegistry assemblyManager,
            IChildContainerBuilder childContainerBuilder,
            IOptions<BlazorModuleOptions> options,
            ILoggerFactory loggerFactory)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (assemblyManager is null)
                throw new ArgumentNullException(nameof(assemblyManager));

            if (childContainerBuilder is null)
                throw new ArgumentNullException(nameof(childContainerBuilder));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            if (loggerFactory is null)
                throw new ArgumentNullException(nameof(loggerFactory));

            ModuleDescriptor = moduleDescriptor;
            _assemblyManager = assemblyManager;
            _childContainerBuilder = childContainerBuilder;
            _options = options;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<BlazorModuleInstaller>();
        }

        public IBlazorModuleDescriptor ModuleDescriptor { get; }

        public bool IsInstalled => _module != null;

        public async ValueTask InstallAsync(CancellationToken cancellation)
        {
            if (IsInstalled)
            {
                return;
            }

            var moduleContext = await BuildModuleContextAsync(cancellation);
            var componentAssemblies = GetComponentAssemblies(moduleContext);
            var moduleServices = _childContainerBuilder.CreateChildContainer(
                services => ConfigureServices(services, moduleContext, componentAssemblies));

            await InitializeApplicationServicesAsync(moduleServices, cancellation).ConfigureAwait(false);

            _assemblyManager.AddAssemblies(
                componentAssemblies.Select(p => moduleContext.ModuleReflectionContext.MapAssembly(p)),
                moduleContext.ModuleLoadContext,
                new WeakServiceProvider(moduleServices));

            _module = new BlazorModule(
                moduleContext.ModuleLoadContext,
                moduleContext.ModuleReflectionContext,
                moduleServices);
        }

        private static Task InitializeApplicationServicesAsync(
            IChildServiceProvider moduleServices,
            CancellationToken cancellation)
        {
            var appServices = moduleServices.GetService<ApplicationServiceManager>();

            if (appServices is null)
                return Task.CompletedTask;

            return appServices.InitializeApplicationServicesAsync(moduleServices, cancellation);
        }

        private void ConfigureServices(
            IServiceCollection services,
            ModuleContext moduleContext,
            ImmutableList<Assembly> componentAssemblies)
        {
            // Build and attach type-resolver             
            services.AddSingleton(moduleContext.ModuleTypeResolver);

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

        private ImmutableList<Assembly> GetComponentAssemblies(ModuleContext moduleContext)
        {
            Assembly LoadAssembly(IBlazorModuleAssemblyDescriptor moduleAssemblyDescriptor)
            {
                return moduleContext.ModuleLoadContext.LoadFromAssemblyName(moduleAssemblyDescriptor.GetAssemblyName());
            }

            return ModuleDescriptor
                .Assemblies
                .Where(p => p.IsComponentAssembly)
                .Select(LoadAssembly)
                .ToImmutableList();
        }

        private async ValueTask<ModuleContext> BuildModuleContextAsync(CancellationToken cancellation)
        {
            var assemblySources = await PrefetchAssemblySourcesAsync(ModuleDescriptor.Assemblies, cancellation);
            var moduleLoadContext = new BlazorModuleAssemblyLoadContext(
                assemblySources, _loggerFactory.CreateLogger<BlazorModuleAssemblyLoadContext>());
            var moduleReflectionContext = new WeakReflectionContext();
            var moduleTypeResolver = new ReflectionContextTypeResolver(
                        new BlazorModuleTypeResolver(moduleLoadContext), moduleReflectionContext);

            return new ModuleContext(ModuleDescriptor, moduleLoadContext, moduleReflectionContext, moduleTypeResolver);
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
            var installedAssemblies = ((BlazorModuleAssemblyLoadContext)assemblyLoadContext).InstalledAssemblies.ToImmutableHashSet(AssemblyByDisplayNameComparer.Instance);

            // Trigger alc unload
            assemblyLoadContext.Unload();

            // Remove the component assemblies (a subset of the installed assemblies) from the assembly manager 
            // and dispose all module services.
            _assemblyManager.RemoveAssemblies(installedAssemblies);
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
