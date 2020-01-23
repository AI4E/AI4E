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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using AI4E.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class BlazorModuleAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly ImmutableDictionary<AssemblyName, BlazorModuleAssemblySource> _assemblySources;
        private readonly ILogger<BlazorModuleAssemblyLoadContext> _logger;

        private readonly ImmutableHashSet<AssemblyName> _coreAssemblies;
        private ImmutableDictionary<AssemblyName, Assembly>? _assemblyCache;
        private readonly object _mutex = new object();

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

            _coreAssemblies = BuildCoreAssemblies();
            _assemblyCache = ImmutableDictionary.Create<AssemblyName, Assembly>(AssemblyNameComparer.ByDisplayName);

            Unloading += OnUnloading;
        }

        private static ImmutableHashSet<AssemblyName> BuildCoreAssemblies()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(p => GetLoadContext(p) == Default)
                .Select(p => p.GetName())
                .Append(typeof(object).Assembly.GetName())
                .ToImmutableHashSet(AssemblyNameComparer.ByDisplayName);
        }

        private void OnUnloading(AssemblyLoadContext obj)
        {
            Unloading -= OnUnloading;

            lock (_mutex)
            {
                _assemblyCache = null;
            }

            foreach (var source in _assemblySources!.Values)
            {
                source.Dispose();
            }
        }

        public IEnumerable<Assembly> InstalledAssemblies
        {
            get
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

                return assemblyCache.Values;
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

            _logger.LogDebug("Requested loading assembly {0}.", assemblyName);

            if (assemblyCache.TryGetValue(assemblyName, out var assembly))
            {
                _logger.LogDebug("Found requested assembly {0} in cache.", assemblyName);

                return assembly;
            }

            var hasSource = _assemblySources.TryGetValue(assemblyName, out var assemblySource);

            // We have no source => Fallback to core or throw.
            if (!hasSource)
            {
                _logger.LogDebug("Requested assembly {0} has no source. Falling back to default context.", assemblyName);
                return null;
            }

            // We either have no source 
            // or we have a source but are not forces to load the assembly in the current context.
            if (!assemblySource.ForceLoad)
            {
                if (_coreAssemblies.Contains(assemblyName))
                {
                    _logger.LogDebug("Requested assembly {0} has matching core assembly. Falling back to core assembly.", assemblyName);
                    return null;
                }
            }

            _logger.LogDebug("Loading requested assembly {0} from source into current context.", assemblyName);

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

                _assemblyCache = _assemblyCache.Add(assemblyName, assembly);
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
