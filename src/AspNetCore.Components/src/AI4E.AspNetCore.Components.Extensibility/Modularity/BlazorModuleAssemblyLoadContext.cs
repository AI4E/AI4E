﻿/* License
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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using AI4E.Utils;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> _assemblySources;
        private readonly ILogger<BlazorModuleAssemblyLoadContext> _logger;

        private ImmutableDictionary<AssemblyName, Assembly>? _assemblyCache;
        private ImmutableHashSet<Assembly>? _installedAssemblies;
        private readonly object _mutex = new object();

        private readonly ImmutableHashSet<AssemblyName> _forcedLoadFromCoreAssemblies;

        public BlazorModuleAssemblyLoadContext(
            ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> assemblySources,
            ILogger<BlazorModuleAssemblyLoadContext>? logger = null)
#if SUPPORTS_COLLECTIBLE_ASSEMBLY_LOAD_CONTEXT
                : base(isCollectible: true)
#endif
        {
            if (assemblySources is null)
                throw new ArgumentNullException(nameof(assemblySources));

            _assemblySources = assemblySources;
            _logger = logger ?? NullLogger<BlazorModuleAssemblyLoadContext>.Instance;

            _assemblyCache = ImmutableDictionary.Create<AssemblyName, Assembly>(AssemblyNameComparer.ByDisplayName);
            _installedAssemblies = ImmutableHashSet.Create<Assembly>(AssemblyByDisplayNameComparer.Instance);

            Unloading += OnUnloading;

            // TODO: This should also be configurable
            _forcedLoadFromCoreAssemblies = BuildForcedLoadFromCoreAssemblies().ToImmutableHashSet(
                AssemblyNameComparer.BySimpleName);
        }

        private static IEnumerable<AssemblyName> BuildForcedLoadFromCoreAssemblies()
        {
            yield return new AssemblyName("netstandard");
            yield return typeof(object).Assembly.GetName();
            yield return typeof(IServiceCollection).Assembly.GetName();
            yield return typeof(IComponent).Assembly.GetName();
        }

        private void OnUnloading(AssemblyLoadContext obj)
        {
            Unloading -= OnUnloading;

            lock (_mutex)
            {
                _assemblyCache = null;
                _installedAssemblies = null;
            }

            foreach (var source in _assemblySources!.Values)
            {
                source.Dispose();
            }
        }

        public ImmutableHashSet<Assembly> InstalledAssemblies
        {
            get
            {
                ImmutableHashSet<Assembly>? installedAssemblies;

                lock (_mutex)
                {
                    installedAssemblies = _installedAssemblies;
                }

                if (installedAssemblies is null)
                {
                    ThrowUnloadingException();
                }

                return installedAssemblies;
            }
        }

        private static string FormatAssemblyName(AssemblyName assemblyName)
        {
            return "'" + assemblyName.Name + ", " + assemblyName.Version + "'";
        }

        private bool TryLoadFromCore(AssemblyName assemblyName, [NotNullWhen(true)] out Assembly? assembly)
        {
            try
            {
                assembly = Default.LoadFromAssemblyName(assemblyName);
                return true;
            }
            catch (FileNotFoundException)
            {
                assembly = null;
                return false;
            }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            ImmutableDictionary<AssemblyName, Assembly>? assemblyCache;

            lock (_mutex)
            {
                assemblyCache = _assemblyCache;
            }

            if (assemblyCache is null)
            {
                ThrowUnloadingException();
            }

            // The assembly is in the cache.
            if (assemblyCache.TryGetValue(assemblyName, out var assembly))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Found requested assembly {0} in cache.", FormatAssemblyName(assemblyName));

                return assembly;
            }

            // We are forced to load the assembly from the default context.
            if (_forcedLoadFromCoreAssemblies.Contains(assemblyName))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Requested assembly {0} is force to be loaded from the default context.", FormatAssemblyName(assemblyName));

                return null;
            }

            // We have no source.
            if (!_assemblySources.TryGetValue(assemblyName, out var assemblySource))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Requested assembly {0} has no source. Falling back to default context.", FormatAssemblyName(assemblyName));

                return null;
            }

            // We are not force´d to load the assembly in the current context. Try the default context.
            if (!assemblySource.ForceLoad)
            {
                if (TryLoadFromCore(assemblyName, out assembly))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Requested assembly {0} has matching core assembly. Falling back to core assembly.", FormatAssemblyName(assemblyName));

                    return assembly;
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Loading requested assembly {0} from source into current context.", FormatAssemblyName(assemblyName));

            try
            {
                assembly = assemblySource.Load(this);
            }
            catch (ObjectDisposedException exc)
            {
                ThrowUnloadingException(exc);
            }

            lock (_mutex)
            {
                if (_assemblyCache is null)
                {
                    ThrowUnloadingException();
                }

                Debug.Assert(_installedAssemblies != null);

                _assemblyCache = _assemblyCache.Add(assemblyName, assembly);
                _installedAssemblies = _installedAssemblies!.Add(assembly);
            }

            return assembly;
        }

        [DoesNotReturn]
        private static void ThrowUnloadingException(Exception? innerException = null)
        {
            if (innerException is null)
            {
                throw new Exception("Unable to load an assembly while unloading the assembly load context.", innerException);
            }
            else
            {
                throw new Exception("Unable to load an assembly while unloading the assembly load context.");
            }
        }
    }
}
